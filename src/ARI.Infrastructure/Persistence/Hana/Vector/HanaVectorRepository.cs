using System.Data;
using System.Data.Common;
using System.Text.Json;
using ARI.Core.Contracts;
using ARI.Infrastructure.AI.Embeddings;

namespace ARI.Infrastructure.Persistence.Hana.Vector;

public sealed record RepairEmbeddingRecord(
    Guid Id,
    Guid RepairOrderId,
    int ChunkIndex,
    IReadOnlyList<float> Vector,
    string EmbeddingModel,
    string ModelVersion,
    string SourceSha256,
    DateTimeOffset SourceAvailableAtUtc);

public interface IHanaVectorRepository
{
    Task<IReadOnlyDictionary<Guid, string>> GetSourceHashesAsync(
        string embeddingModel,
        string modelVersion,
        CancellationToken cancellationToken = default);

    Task StoreAsync(
        IReadOnlyList<RepairEmbeddingRecord> records,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SimilarRepairCase>> SearchAsync(
        RepairRetrievalQuery query,
        IReadOnlyList<float>? queryVector,
        EmbeddingProviderOptions embeddingOptions,
        CancellationToken cancellationToken = default);
}

public sealed class HanaVectorRepository(
    IHanaConnectionFactory connectionFactory,
    HanaOptions hanaOptions) : IHanaVectorRepository
{
    public async Task<IReadOnlyDictionary<Guid, string>> GetSourceHashesAsync(
        string embeddingModel,
        string modelVersion,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT REPAIR_ORDER_ID, SOURCE_SHA256
            FROM ARI.REPAIR_REPORT_EMBEDDINGS
            WHERE EMBEDDING_MODEL = ? AND MODEL_VERSION = ? AND CHUNK_INDEX = 0
            """;
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = HanaCommand.Create(
            connection, sql, hanaOptions.ConnectionTimeoutSeconds, null,
            (DbType.String, embeddingModel),
            (DbType.String, modelVersion));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new Dictionary<Guid, string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result[Guid.Parse(reader.GetString(0))] = reader.GetString(1);
        }

        return result;
    }

    public async Task StoreAsync(
        IReadOnlyList<RepairEmbeddingRecord> records,
        CancellationToken cancellationToken = default)
    {
        if (records.Count == 0)
        {
            return;
        }

        if (records.Any(record => record.Vector.Count != 1536 ||
                                  record.Vector.Any(value => !float.IsFinite(value))))
        {
            throw new ArgumentException("Every stored vector must contain 1536 finite values.", nameof(records));
        }

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var record in records)
            {
                const string deleteSql = """
                    DELETE FROM ARI.REPAIR_REPORT_EMBEDDINGS
                    WHERE REPAIR_ORDER_ID = ? AND CHUNK_INDEX = ?
                      AND EMBEDDING_MODEL = ? AND MODEL_VERSION = ?
                    """;
                await using (var delete = HanaCommand.Create(
                                 connection, deleteSql, hanaOptions.ConnectionTimeoutSeconds, transaction,
                                 (DbType.String, record.RepairOrderId.ToString("D")),
                                 (DbType.Int32, record.ChunkIndex),
                                 (DbType.String, record.EmbeddingModel),
                                 (DbType.String, record.ModelVersion)))
                {
                    await delete.ExecuteNonQueryAsync(cancellationToken);
                }

                const string insertSql = """
                    INSERT INTO ARI.REPAIR_REPORT_EMBEDDINGS
                        (ID, REPAIR_ORDER_ID, CHUNK_INDEX, EMBEDDING, EMBEDDING_MODEL,
                         MODEL_VERSION, SOURCE_SHA256, SOURCE_AVAILABLE_AT_UTC, CREATED_AT_UTC)
                    VALUES (?, ?, ?, TO_REAL_VECTOR(?), ?, ?, ?, ?, CURRENT_UTCTIMESTAMP)
                    """;
                await using var insert = HanaCommand.Create(
                    connection, insertSql, hanaOptions.ConnectionTimeoutSeconds, transaction,
                    (DbType.String, record.Id.ToString("D")),
                    (DbType.String, record.RepairOrderId.ToString("D")),
                    (DbType.Int32, record.ChunkIndex),
                    (DbType.String, JsonSerializer.Serialize(record.Vector)),
                    (DbType.String, record.EmbeddingModel),
                    (DbType.String, record.ModelVersion),
                    (DbType.String, record.SourceSha256),
                    (DbType.DateTime, HanaValueMapper.ToDatabaseTimestamp(record.SourceAvailableAtUtc)));
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<IReadOnlyList<SimilarRepairCase>> SearchAsync(
        RepairRetrievalQuery query,
        IReadOnlyList<float>? queryVector,
        EmbeddingProviderOptions embeddingOptions,
        CancellationToken cancellationToken = default)
    {
        ValidateSearch(query, queryVector, embeddingOptions);
        var sql = BuildSearchSql(query);
        var parameters = BuildParameters(query, queryVector, embeddingOptions);
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = HanaCommand.Create(
            connection, sql, hanaOptions.ConnectionTimeoutSeconds, null, parameters.ToArray());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<SimilarRepairCase>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var score = Convert.ToDouble(reader.GetValue(1));
            var similarity = reader.IsDBNull(2) ? (double?)null : Convert.ToDouble(reader.GetValue(2));
            if (similarity is not null && similarity < query.MinimumSimilarity)
            {
                continue;
            }

            var orderId = Guid.Parse(reader.GetString(0));
            var report = reader.GetString(12);
            results.Add(new SimilarRepairCase(
                orderId,
                score,
                similarity,
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetInt32(6),
                reader.GetInt32(7),
                Convert.ToDecimal(reader.GetValue(8)),
                reader.GetInt32(9),
                RepairDocumentTypes.RepairCompletionReport,
                reader.GetString(10),
                $"repair-order:{orderId:D}",
                report.Length <= 600 ? report : report[..600],
                HanaValueMapper.FromDatabaseTimestamp(reader.GetDateTime(11))));
            if (results.Count == query.TopK)
            {
                break;
            }
        }

        return results;
    }

    private static string BuildSearchSql(RepairRetrievalQuery query)
    {
        var candidateCount = Math.Min(100, query.TopK * 5);
        var vectorExpression = "COSINE_SIMILARITY(E.EMBEDDING, TO_REAL_VECTOR(?))";
        var score = query.Strategy switch
        {
            RepairRetrievalStrategies.Structured =>
                "(CASE WHEN RO.REPAIR_CODE = ? THEN 0.50 ELSE 0 END + " +
                "CASE WHEN VS.MANUFACTURER = ? THEN 0.10 ELSE 0 END + " +
                "CASE WHEN VS.MODEL = ? THEN 0.15 ELSE 0 END + " +
                "CASE WHEN ABS(VS.MODEL_YEAR - ?) <= 3 THEN 0.10 ELSE 0 END + " +
                "CASE WHEN ABS(VS.MILEAGE_KM - ?) <= 50000 THEN 0.10 ELSE 0 END + " +
                "CASE WHEN EXISTS (SELECT 1 FROM ARI.REPAIR_CONDITIONS RC " +
                "WHERE RC.REPAIR_ORDER_ID = RO.ID AND RC.CONDITION_CODE = ?) THEN 0.05 ELSE 0 END)",
            RepairRetrievalStrategies.Semantic => vectorExpression,
            _ => $"(0.70 * {vectorExpression} + " +
                 "CASE WHEN VS.MANUFACTURER = ? THEN 0.05 ELSE 0 END + " +
                 "CASE WHEN VS.MODEL = ? THEN 0.08 ELSE 0 END + " +
                 "CASE WHEN ABS(VS.MODEL_YEAR - ?) <= 3 THEN 0.06 ELSE 0 END + " +
                 "CASE WHEN ABS(VS.MILEAGE_KM - ?) <= 50000 THEN 0.06 ELSE 0 END + " +
                 "CASE WHEN EXISTS (SELECT 1 FROM ARI.REPAIR_CONDITIONS RC " +
                 "WHERE RC.REPAIR_ORDER_ID = RO.ID AND RC.CONDITION_CODE = ?) THEN 0.05 ELSE 0 END)"
        };
        var vectorSimilarity = query.Strategy == RepairRetrievalStrategies.Structured
            ? "CAST(NULL AS DOUBLE)"
            : vectorExpression;
        var repairFilter = query.Strategy != RepairRetrievalStrategies.Semantic
            ? " AND RO.REPAIR_CODE = ?"
            : string.Empty;
        return $"""
            SELECT TOP {candidateCount}
                E.REPAIR_ORDER_ID,
                {score} AS SCORE,
                {vectorSimilarity} AS VECTOR_SIMILARITY,
                RO.REPAIR_CODE,
                VS.MANUFACTURER,
                VS.MODEL,
                VS.MODEL_YEAR,
                VS.MILEAGE_KM,
                (SELECT SUM(OP.REFERENCE_LABOR_HOURS)
                   FROM ARI.REPAIR_OPERATIONS OP WHERE OP.REPAIR_ORDER_ID = RO.ID) AS BASELINE_HOURS,
                RX.ACTUAL_LABOR_MINUTES,
                HD.DATA_CLASSIFICATION,
                E.SOURCE_AVAILABLE_AT_UTC,
                RX.REPAIR_REPORT
            FROM ARI.REPAIR_REPORT_EMBEDDINGS E
            INNER JOIN ARI.REPAIR_ORDERS RO ON RO.ID = E.REPAIR_ORDER_ID
            INNER JOIN ARI.VEHICLE_SNAPSHOTS VS ON VS.ID = RO.VEHICLE_SNAPSHOT_ID
            INNER JOIN ARI.REPAIR_EXECUTIONS RX ON RX.REPAIR_ORDER_ID = RO.ID
            INNER JOIN ARI.HISTORICAL_DATASETS HD ON HD.DATASET_ID = RO.DATASET_ID
            WHERE E.EMBEDDING_MODEL = ? AND E.MODEL_VERSION = ?
              AND E.SOURCE_AVAILABLE_AT_UTC <= ?{repairFilter}
            ORDER BY SCORE DESC, E.REPAIR_ORDER_ID
            """;
    }

    private static List<(DbType Type, object? Value)> BuildParameters(
        RepairRetrievalQuery query,
        IReadOnlyList<float>? queryVector,
        EmbeddingProviderOptions options)
    {
        var values = new List<(DbType, object?)>();
        var vectorJson = queryVector is null ? null : JsonSerializer.Serialize(queryVector);
        var expectedYear = query.VehicleAgeYears is null
            ? DateTimeOffset.UtcNow.Year
            : query.AsOfUtc.Year - query.VehicleAgeYears.Value;
        var mileage = query.MileageKm ?? 0;
        var manufacturer = query.Manufacturer ?? string.Empty;
        var model = query.Model ?? string.Empty;
        var condition = query.KnownConditions.FirstOrDefault() ?? string.Empty;

        if (query.Strategy == RepairRetrievalStrategies.Structured)
        {
            values.Add((DbType.String, query.RepairCode));
            values.Add((DbType.String, manufacturer));
            values.Add((DbType.String, model));
            values.Add((DbType.Int32, expectedYear));
            values.Add((DbType.Int32, mileage));
            values.Add((DbType.String, condition));
        }
        else
        {
            values.Add((DbType.String, vectorJson));
            if (query.Strategy == RepairRetrievalStrategies.Hybrid)
            {
                values.Add((DbType.String, manufacturer));
                values.Add((DbType.String, model));
                values.Add((DbType.Int32, expectedYear));
                values.Add((DbType.Int32, mileage));
                values.Add((DbType.String, condition));
            }

            values.Add((DbType.String, vectorJson));
        }

        values.Add((DbType.String, options.Model));
        values.Add((DbType.String, options.ModelVersion));
        values.Add((DbType.DateTime, HanaValueMapper.ToDatabaseTimestamp(query.AsOfUtc)));
        if (query.Strategy != RepairRetrievalStrategies.Semantic)
        {
            values.Add((DbType.String, query.RepairCode));
        }

        return values;
    }

    private static void ValidateSearch(
        RepairRetrievalQuery query,
        IReadOnlyList<float>? vector,
        EmbeddingProviderOptions options)
    {
        if (!RepairRetrievalStrategies.IsSupported(query.Strategy) ||
            query.TopK is < 1 or > 20 ||
            query.MinimumSimilarity is < -1 or > 1 ||
            string.IsNullOrWhiteSpace(query.Description) ||
            string.IsNullOrWhiteSpace(query.RepairCode))
        {
            throw new ArgumentException("Retrieval query is invalid.", nameof(query));
        }

        if (query.Strategy != RepairRetrievalStrategies.Structured &&
            (vector is null || vector.Count != options.Dimensions ||
             vector.Any(value => !float.IsFinite(value))))
        {
            throw new ArgumentException("Semantic retrieval requires a valid configured query vector.", nameof(vector));
        }
    }
}
