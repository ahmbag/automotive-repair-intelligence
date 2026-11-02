using System.Data;
using System.Data.Common;
using System.Text.Json;
using ARI.Core.Contracts;
using ARI.Infrastructure.AI.Embeddings;

namespace ARI.Infrastructure.Persistence.Hana.Vector;

public sealed record RepairSearchEmbeddingRecord(
    Guid Id,
    Guid RepairOrderId,
    string ProfileType,
    int ChunkIndex,
    IReadOnlyList<float> Vector,
    string EmbeddingModel,
    string ModelVersion,
    string ProfileVersion,
    string DocumentVersion,
    string SourceSha256,
    DateTimeOffset SourceAvailableAtUtc);

public interface IHanaRepairSearchV2Repository
{
    Task<IReadOnlyDictionary<string, string>> GetSourceHashesAsync(
        string embeddingModel, string modelVersion, string profileVersion,
        CancellationToken cancellationToken = default);

    Task StoreAsync(IReadOnlyList<RepairSearchEmbeddingRecord> records,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SimilarRepairCase>> SearchAsync(
        RepairRetrievalQuery query, IReadOnlyList<float>? queryVector,
        EmbeddingProviderOptions options, CancellationToken cancellationToken = default);
}

public sealed class HanaRepairSearchV2Repository(
    IHanaConnectionFactory connectionFactory,
    HanaOptions hanaOptions) : IHanaRepairSearchV2Repository
{
    public async Task<IReadOnlyDictionary<string, string>> GetSourceHashesAsync(
        string embeddingModel, string modelVersion, string profileVersion,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT REPAIR_ORDER_ID, PROFILE_TYPE, SOURCE_SHA256
            FROM ARI.REPAIR_SEARCH_EMBEDDINGS_V2
            WHERE EMBEDDING_MODEL = ? AND MODEL_VERSION = ? AND PROFILE_VERSION = ? AND CHUNK_INDEX = 0
            """;
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = HanaCommand.Create(connection, sql, hanaOptions.ConnectionTimeoutSeconds, null,
            (DbType.String, embeddingModel), (DbType.String, modelVersion), (DbType.String, profileVersion));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken))
        {
            hashes[$"{reader.GetString(0)}|{reader.GetString(1)}"] = reader.GetString(2);
        }
        return hashes;
    }

    public async Task StoreAsync(IReadOnlyList<RepairSearchEmbeddingRecord> records,
        CancellationToken cancellationToken = default)
    {
        if (records.Count == 0) return;
        if (records.Any(value => value.Vector.Count != 1536 || value.Vector.Any(item => !float.IsFinite(item))))
            throw new ArgumentException("Every V2 vector must contain 1536 finite values.", nameof(records));
        if (records.Any(value => value.ProfileType is not (RepairSearchDocumentTypes.PreRepairProfile or RepairSearchDocumentTypes.CompletionEvidence)))
            throw new ArgumentException("The V2 profile type is invalid.", nameof(records));

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var record in records)
            {
                const string deleteSql = """
                    DELETE FROM ARI.REPAIR_SEARCH_EMBEDDINGS_V2
                    WHERE REPAIR_ORDER_ID = ? AND PROFILE_TYPE = ? AND CHUNK_INDEX = ?
                      AND EMBEDDING_MODEL = ? AND MODEL_VERSION = ? AND PROFILE_VERSION = ?
                    """;
                await using (var delete = HanaCommand.Create(connection, deleteSql,
                                 hanaOptions.ConnectionTimeoutSeconds, transaction,
                                 (DbType.String, record.RepairOrderId.ToString("D")),
                                 (DbType.String, record.ProfileType), (DbType.Int32, record.ChunkIndex),
                                 (DbType.String, record.EmbeddingModel), (DbType.String, record.ModelVersion),
                                 (DbType.String, record.ProfileVersion)))
                    await delete.ExecuteNonQueryAsync(cancellationToken);

                const string insertSql = """
                    INSERT INTO ARI.REPAIR_SEARCH_EMBEDDINGS_V2
                      (ID, REPAIR_ORDER_ID, PROFILE_TYPE, CHUNK_INDEX, EMBEDDING, EMBEDDING_MODEL,
                       MODEL_VERSION, PROFILE_VERSION, DOCUMENT_VERSION, SOURCE_SHA256,
                       SOURCE_AVAILABLE_AT_UTC, CREATED_AT_UTC)
                    VALUES (?, ?, ?, ?, TO_REAL_VECTOR(?), ?, ?, ?, ?, ?, ?, CURRENT_UTCTIMESTAMP)
                    """;
                await using var insert = HanaCommand.Create(connection, insertSql,
                    hanaOptions.ConnectionTimeoutSeconds, transaction,
                    (DbType.String, record.Id.ToString("D")),
                    (DbType.String, record.RepairOrderId.ToString("D")),
                    (DbType.String, record.ProfileType), (DbType.Int32, record.ChunkIndex),
                    (DbType.String, JsonSerializer.Serialize(record.Vector)),
                    (DbType.String, record.EmbeddingModel), (DbType.String, record.ModelVersion),
                    (DbType.String, record.ProfileVersion), (DbType.String, record.DocumentVersion),
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
        RepairRetrievalQuery query, IReadOnlyList<float>? queryVector,
        EmbeddingProviderOptions options, CancellationToken cancellationToken = default)
    {
        Validate(query, queryVector, options);
        var vectorSql = queryVector is null ? "CAST(NULL AS DOUBLE)" :
            "COSINE_SIMILARITY(E.EMBEDDING, TO_REAL_VECTOR(?))";
        var sql = $"""
            SELECT E.REPAIR_ORDER_ID, {vectorSql} AS VECTOR_SIMILARITY,
                   RO.REPAIR_CODE, VS.MANUFACTURER, VS.MODEL, VS.MODEL_YEAR, VS.MILEAGE_KM,
                   (SELECT SUM(OP.REFERENCE_LABOR_HOURS) FROM ARI.REPAIR_OPERATIONS OP
                     WHERE OP.REPAIR_ORDER_ID = RO.ID),
                   RX.ACTUAL_LABOR_MINUTES, HD.DATA_CLASSIFICATION,
                   E.SOURCE_AVAILABLE_AT_UTC, RX.REPAIR_REPORT,
                   (SELECT STRING_AGG(RC.CONDITION_CODE, ',') FROM ARI.REPAIR_CONDITIONS RC
                     WHERE RC.REPAIR_ORDER_ID = RO.ID)
            FROM ARI.REPAIR_SEARCH_EMBEDDINGS_V2 E
            INNER JOIN ARI.REPAIR_ORDERS RO ON RO.ID = E.REPAIR_ORDER_ID
            INNER JOIN ARI.VEHICLE_SNAPSHOTS VS ON VS.ID = RO.VEHICLE_SNAPSHOT_ID
            INNER JOIN ARI.REPAIR_EXECUTIONS RX ON RX.REPAIR_ORDER_ID = RO.ID
            INNER JOIN ARI.HISTORICAL_DATASETS HD ON HD.DATASET_ID = RO.DATASET_ID
            WHERE E.EMBEDDING_MODEL = ? AND E.MODEL_VERSION = ? AND E.PROFILE_VERSION = ?
              AND E.DOCUMENT_VERSION = ? AND E.PROFILE_TYPE = ?
              AND E.SOURCE_AVAILABLE_AT_UTC <= ? AND RX.COMPLETED_AT_UTC <= ?
              AND RO.REPAIR_CODE = ? AND HD.DATA_CLASSIFICATION = 'SYNTHETIC'
            """;
        var parameters = new List<(DbType, object?)>();
        if (queryVector is not null) parameters.Add((DbType.String, JsonSerializer.Serialize(queryVector)));
        parameters.AddRange([
            (DbType.String, options.Model), (DbType.String, options.V2ModelVersion),
            (DbType.String, options.V2ProfileVersion), (DbType.String, options.V2DocumentVersion),
            (DbType.String, RepairSearchDocumentTypes.PreRepairProfile),
            (DbType.DateTime, HanaValueMapper.ToDatabaseTimestamp(query.AsOfUtc)),
            (DbType.DateTime, HanaValueMapper.ToDatabaseTimestamp(query.AsOfUtc)),
            (DbType.String, query.RepairCode)]);

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = HanaCommand.Create(connection, sql, hanaOptions.ConnectionTimeoutSeconds, null,
            parameters.ToArray());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var candidates = new List<Candidate>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var similarity = reader.IsDBNull(1) ? (double?)null : Convert.ToDouble(reader.GetValue(1));
            var conditions = reader.IsDBNull(12) ? [] : reader.GetString(12)
                .Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
            candidates.Add(new Candidate(
                Guid.Parse(reader.GetString(0)), similarity, reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.GetInt32(5), reader.GetInt32(6),
                Convert.ToDecimal(reader.GetValue(7)), reader.GetInt32(8), reader.GetString(9),
                HanaValueMapper.FromDatabaseTimestamp(reader.GetDateTime(10)), reader.GetString(11), conditions));
        }

        var structured = candidates.OrderByDescending(value => StructuredScore(query, value))
            .ThenBy(value => value.Id).ToArray();
        var semantic = candidates.Where(value => value.Similarity >= query.MinimumSimilarity)
            .OrderByDescending(value => value.Similarity).ThenBy(value => value.Id).ToArray();
        var structuredRank = structured.Select((value, index) => (value.Id, Rank: index + 1))
            .ToDictionary(value => value.Id, value => value.Rank);
        var semanticRank = semantic.Select((value, index) => (value.Id, Rank: index + 1))
            .ToDictionary(value => value.Id, value => value.Rank);

        IEnumerable<(Candidate Value, double Score)> ranked = query.Strategy switch
        {
            RepairRetrievalStrategies.DomainSemantic => semantic.Select(value => (value, value.Similarity ?? -1)),
            RepairRetrievalStrategies.StructuredFirstHybrid => candidates.Select(value =>
                (value, 0.72 * NormalizeStructured(StructuredScore(query, value)) + 0.28 * ((value.Similarity ?? -1) + 1) / 2)),
            RepairRetrievalStrategies.RankFusion => candidates.Select(value =>
                (value, 0.90 / (60 + structuredRank[value.Id]) +
                        0.10 / (60 + (semanticRank.TryGetValue(value.Id, out var rank) ? rank : candidates.Count + 1)))),
            _ => structured.Select(value => (value, StructuredScore(query, value)))
        };

        return ranked.OrderByDescending(value => value.Score).ThenBy(value => value.Value.Id)
            .Take(query.TopK).Select(value => ToResult(value.Value, value.Score)).ToArray();
    }

    private static double StructuredScore(RepairRetrievalQuery query, Candidate value)
    {
        var score = 0d;
        if (string.Equals(query.Model, value.Model, StringComparison.OrdinalIgnoreCase)) score += 4;
        if (string.Equals(query.Manufacturer, value.Manufacturer, StringComparison.OrdinalIgnoreCase)) score += 2;
        if (query.MileageKm is not null) score += Math.Max(0, 1 - Math.Abs(query.MileageKm.Value - value.Mileage) / 200_000d);
        if (query.VehicleAgeYears is not null)
        {
            var expectedYear = query.AsOfUtc.Year - query.VehicleAgeYears.Value;
            score += Math.Max(0, 1 - Math.Abs(expectedYear - value.ModelYear) / 10d);
        }
        if (query.KnownConditions.Any(value.Conditions.Contains)) score += 2;
        return score;
    }

    private static double NormalizeStructured(double value) => value / 10d;

    private static SimilarRepairCase ToResult(Candidate value, double score) => new(
        value.Id, score, value.Similarity, value.RepairCode, value.Manufacturer, value.Model,
        value.ModelYear, value.Mileage, value.BaselineHours, value.ActualMinutes,
        RepairSearchDocumentTypes.CompletionEvidence, value.Classification,
        $"repair-order:{value.Id:D}", value.Report.Length <= 600 ? value.Report : value.Report[..600],
        value.AvailableAt);

    private static void Validate(RepairRetrievalQuery query, IReadOnlyList<float>? vector,
        EmbeddingProviderOptions options)
    {
        if (!RepairRetrievalStrategies.IsSupported(query.Strategy) || query.TopK is < 1 or > 20 ||
            query.MinimumSimilarity is < -1 or > 1 || string.IsNullOrWhiteSpace(query.RepairCode) ||
            query.EmbeddingProfile != RepairEmbeddingProfiles.RepairProfileV2)
            throw new ArgumentException("V2 retrieval query is invalid.", nameof(query));
        if (query.Strategy != RepairRetrievalStrategies.Structured &&
            (vector is null || vector.Count != options.Dimensions || vector.Any(value => !float.IsFinite(value))))
            throw new ArgumentException("V2 semantic retrieval requires a valid query vector.", nameof(vector));
    }

    private sealed record Candidate(Guid Id, double? Similarity, string RepairCode,
        string Manufacturer, string Model, int ModelYear, int Mileage, decimal BaselineHours,
        int ActualMinutes, string Classification, DateTimeOffset AvailableAt, string Report,
        IReadOnlySet<string> Conditions);
}
