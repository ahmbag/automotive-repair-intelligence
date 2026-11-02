using System.Data;
using System.Text.Json;
using ARI.Infrastructure.Persistence.Hana;
using ARI.Infrastructure.Persistence.Hana.Import;

namespace ARI.Infrastructure.MachineLearning;

public sealed class HanaTrainingDatasetExporter(
    IHanaConnectionFactory connectionFactory,
    HanaOptions options)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public async Task<TrainingExport> ExportAsync(
        string datasetId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var source = await ReadSourceManifestAsync(connection, datasetId, cancellationToken);
        var complications = await ReadComplicationsAsync(connection, datasetId, cancellationToken);
        var rows = await ReadRowsAsync(connection, datasetId, complications, cancellationToken);

        if (source.Status != "COMPLETE" || rows.Count != source.RecordCount)
        {
            throw new InvalidOperationException(
                "Only complete datasets with one exported row per record may be exported.");
        }

        var checksum = ComputeChecksum(rows);
        return new TrainingExport(
            new TrainingExportManifest(
                datasetId,
                source.GeneratorVersion,
                source.Checksum,
                rows.Count,
                source.Classification,
                1,
                "ari-labor-features-v1",
                checksum),
            rows);
    }

    public static async Task WriteAsync(
        TrainingExport export,
        string path,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var json = JsonSerializer.Serialize(export, JsonOptions) + Environment.NewLine;
        await File.WriteAllTextAsync(fullPath, json, cancellationToken);
    }

    private async Task<SourceManifest> ReadSourceManifestAsync(
        System.Data.Common.DbConnection connection,
        string datasetId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT GENERATOR_VERSION, CHECKSUM, EXPECTED_RECORD_COUNT,
                   DATA_CLASSIFICATION, IMPORT_STATUS
            FROM ARI.HISTORICAL_DATASETS
            WHERE DATASET_ID = ?
            """;
        await using var command = HanaCommand.Create(
            connection, sql, options.ConnectionTimeoutSeconds, null,
            (DbType.String, datasetId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("The requested historical dataset does not exist.");
        }

        return new SourceManifest(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetInt32(2),
            reader.GetString(3),
            reader.GetString(4));
    }

    private async Task<Dictionary<Guid, List<string>>> ReadComplicationsAsync(
        System.Data.Common.DbConnection connection,
        string datasetId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT r.ID, c.COMPLICATION_CODE
            FROM ARI.REPAIR_ORDERS r
            JOIN ARI.REPAIR_EXECUTIONS e ON e.REPAIR_ORDER_ID = r.ID
            JOIN ARI.REPAIR_COMPLICATIONS c ON c.REPAIR_EXECUTION_ID = e.ID
            WHERE r.DATASET_ID = ?
            ORDER BY r.ID, c.COMPLICATION_CODE
            """;
        await using var command = HanaCommand.Create(
            connection, sql, options.ConnectionTimeoutSeconds, null,
            (DbType.String, datasetId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new Dictionary<Guid, List<string>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var orderId = Guid.Parse(reader.GetString(0));
            if (!result.TryGetValue(orderId, out var values))
            {
                values = [];
                result.Add(orderId, values);
            }

            values.Add(reader.GetString(1));
        }

        return result;
    }

    private async Task<IReadOnlyList<TrainingExportRow>> ReadRowsAsync(
        System.Data.Common.DbConnection connection,
        string datasetId,
        IReadOnlyDictionary<Guid, List<string>> complications,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT r.ID, o.ID, r.CREATED_AT_UTC, e.COMPLETED_AT_UTC,
                   v.MODEL_YEAR, v.MILEAGE_KM, v.MANUFACTURER, v.MODEL,
                   o.OPERATION_CODE, o.REFERENCE_LABOR_HOURS,
                   CASE WHEN EXISTS (
                       SELECT 1 FROM ARI.REPAIR_CONDITIONS k
                       WHERE k.REPAIR_ORDER_ID = r.ID AND k.CONDITION_CODE = 'VISIBLE_CORROSION'
                   ) THEN 1 ELSE 0 END,
                   e.ACTUAL_LABOR_MINUTES, e.REPAIR_REPORT
            FROM ARI.REPAIR_ORDERS r
            JOIN ARI.VEHICLE_SNAPSHOTS v ON v.ID = r.VEHICLE_SNAPSHOT_ID
            JOIN ARI.REPAIR_OPERATIONS o ON o.REPAIR_ORDER_ID = r.ID
            JOIN ARI.REPAIR_EXECUTIONS e ON e.REPAIR_ORDER_ID = r.ID
            WHERE r.DATASET_ID = ?
            ORDER BY e.COMPLETED_AT_UTC, r.ID, o.SEQUENCE_NUMBER
            """;
        await using var command = HanaCommand.Create(
            connection, sql, options.ConnectionTimeoutSeconds, null,
            (DbType.String, datasetId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<TrainingExportRow>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var orderId = Guid.Parse(reader.GetString(0));
            var completedAt = HanaValueMapper.FromDatabaseTimestamp(reader.GetDateTime(3));
            var modelYear = reader.GetInt32(4);
            var age = Math.Max(0, completedAt.Year - modelYear);
            var baselineMinutes = reader.GetDecimal(9) * 60m;
            if (baselineMinutes != decimal.Truncate(baselineMinutes))
            {
                throw new InvalidOperationException(
                    "ARI-ML-01 export requires whole baseline labor minutes.");
            }

            result.Add(new TrainingExportRow(
                orderId,
                Guid.Parse(reader.GetString(1)),
                HanaValueMapper.FromDatabaseTimestamp(reader.GetDateTime(2)),
                completedAt,
                age,
                reader.GetInt32(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                decimal.ToInt32(baselineMinutes),
                Convert.ToInt32(reader.GetValue(10)) == 1,
                reader.GetInt32(11),
                complications.TryGetValue(orderId, out var codes) ? codes : [],
                reader.GetString(12)));
        }

        return result;
    }

    private static string ComputeChecksum(IReadOnlyList<TrainingExportRow> rows)
    {
        var json = JsonSerializer.Serialize(rows, JsonOptions);
        using var document = JsonDocument.Parse(json);
        return HistoricalDatasetJsonReader.ComputeCanonicalChecksum(document.RootElement);
    }

    private sealed record SourceManifest(
        string GeneratorVersion,
        string Checksum,
        int RecordCount,
        string Classification,
        string Status);
}
