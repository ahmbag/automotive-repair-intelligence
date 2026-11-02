using System.Data;
using System.Data.Common;

namespace ARI.Infrastructure.Persistence.Hana.Analytics;

public sealed class HanaHistoricalDataProfiler(
    IHanaConnectionFactory connectionFactory,
    HanaOptions options)
{
    public async Task<HistoricalDataProfile> AnalyzeAsync(
        string datasetId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var manifest = await ReadManifestAsync(connection, datasetId, cancellationToken);
        var counts = await ReadCountsAsync(connection, datasetId, cancellationToken);
        var quality = await ReadQualityCountsAsync(connection, datasetId, cancellationToken);
        var laborRows = await ReadLaborRowsAsync(connection, datasetId, cancellationToken);
        var complicationCounts = await ReadComplicationCountsAsync(
            connection, datasetId, cancellationToken);

        var labor = CalculateLaborStatistics(laborRows);
        var corrosion = CalculateCorrosionComparison(laborRows);
        var errors = BuildErrors(manifest, counts, quality, laborRows.Count);

        return new HistoricalDataProfile(
            datasetId,
            manifest.Status,
            manifest.Classification,
            manifest.Expected,
            manifest.Imported,
            counts.Orders,
            counts.Complete,
            counts.Operations,
            counts.Executions,
            counts.Complications,
            counts.Conditions,
            quality.Orphans,
            quality.InvalidLabor,
            quality.InvalidVehicles,
            quality.InvalidTimeline,
            quality.MissingReports,
            labor,
            corrosion,
            complicationCounts,
            errors);
    }

    private async Task<ManifestRow> ReadManifestAsync(
        DbConnection connection,
        string datasetId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXPECTED_RECORD_COUNT, IMPORTED_RECORD_COUNT, DATA_CLASSIFICATION, IMPORT_STATUS
            FROM ARI.HISTORICAL_DATASETS WHERE DATASET_ID = ?
            """;
        await using var command = Create(connection, sql, datasetId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Dataset is not registered in SAP HANA.");
        }

        return new ManifestRow(
            reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3));
    }

    private async Task<CountRow> ReadCountsAsync(
        DbConnection connection,
        string datasetId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                COUNT(DISTINCT r.ID),
                COUNT(DISTINCT CASE WHEN o.ID IS NOT NULL AND e.ID IS NOT NULL THEN r.ID END),
                COUNT(DISTINCT o.ID),
                COUNT(DISTINCT e.ID),
                COUNT(DISTINCT c.ID),
                COUNT(DISTINCT k.REPAIR_ORDER_ID || ':' || k.CONDITION_CODE)
            FROM ARI.REPAIR_ORDERS r
            LEFT JOIN ARI.REPAIR_OPERATIONS o ON o.REPAIR_ORDER_ID = r.ID
            LEFT JOIN ARI.REPAIR_EXECUTIONS e ON e.REPAIR_ORDER_ID = r.ID
            LEFT JOIN ARI.REPAIR_COMPLICATIONS c ON c.REPAIR_EXECUTION_ID = e.ID
            LEFT JOIN ARI.REPAIR_CONDITIONS k ON k.REPAIR_ORDER_ID = r.ID
            WHERE r.DATASET_ID = ?
            """;
        await using var command = Create(connection, sql, datasetId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new CountRow(
            Convert.ToInt32(reader.GetValue(0)),
            Convert.ToInt32(reader.GetValue(1)),
            Convert.ToInt32(reader.GetValue(2)),
            Convert.ToInt32(reader.GetValue(3)),
            Convert.ToInt32(reader.GetValue(4)),
            Convert.ToInt32(reader.GetValue(5)));
    }

    private async Task<QualityRow> ReadQualityCountsAsync(
        DbConnection connection,
        string datasetId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
              (SELECT COUNT(*) FROM ARI.REPAIR_OPERATIONS o
               WHERE NOT EXISTS (SELECT 1 FROM ARI.REPAIR_ORDERS r WHERE r.ID = o.REPAIR_ORDER_ID))
              + (SELECT COUNT(*) FROM ARI.REPAIR_EXECUTIONS e
                 WHERE NOT EXISTS (SELECT 1 FROM ARI.REPAIR_ORDERS r WHERE r.ID = e.REPAIR_ORDER_ID))
              + (SELECT COUNT(*) FROM ARI.REPAIR_COMPLICATIONS c
                 WHERE NOT EXISTS (SELECT 1 FROM ARI.REPAIR_EXECUTIONS e WHERE e.ID = c.REPAIR_EXECUTION_ID)),
              (SELECT COUNT(*) FROM ARI.REPAIR_ORDERS r
               JOIN ARI.REPAIR_OPERATIONS o ON o.REPAIR_ORDER_ID = r.ID
               JOIN ARI.REPAIR_EXECUTIONS e ON e.REPAIR_ORDER_ID = r.ID
               WHERE r.DATASET_ID = ? AND (o.REFERENCE_LABOR_HOURS <= 0 OR e.ACTUAL_LABOR_MINUTES <= 0)),
              (SELECT COUNT(*) FROM ARI.REPAIR_ORDERS r
               JOIN ARI.VEHICLE_SNAPSHOTS v ON v.ID = r.VEHICLE_SNAPSHOT_ID
               JOIN ARI.REPAIR_EXECUTIONS e ON e.REPAIR_ORDER_ID = r.ID
               WHERE r.DATASET_ID = ? AND (v.MODEL_YEAR < 1990 OR v.MODEL_YEAR > YEAR(e.STARTED_AT_UTC)
                    OR v.MILEAGE_KM < 0 OR v.MILEAGE_KM > 1000000)),
              (SELECT COUNT(*) FROM ARI.REPAIR_ORDERS r
               JOIN ARI.VEHICLE_SNAPSHOTS v ON v.ID = r.VEHICLE_SNAPSHOT_ID
               JOIN ARI.REPAIR_EXECUTIONS e ON e.REPAIR_ORDER_ID = r.ID
               WHERE r.DATASET_ID = ? AND (v.CAPTURED_AT_UTC > r.CREATED_AT_UTC
                    OR r.CREATED_AT_UTC > e.STARTED_AT_UTC OR e.STARTED_AT_UTC > e.COMPLETED_AT_UTC))
              + (SELECT COUNT(*) FROM ARI.REPAIR_ORDERS r
                 JOIN ARI.REPAIR_EXECUTIONS e ON e.REPAIR_ORDER_ID = r.ID
                 JOIN ARI.REPAIR_COMPLICATIONS c ON c.REPAIR_EXECUTION_ID = e.ID
                 WHERE r.DATASET_ID = ? AND (c.OCCURRED_AT_UTC < e.STARTED_AT_UTC
                      OR c.OCCURRED_AT_UTC > e.COMPLETED_AT_UTC))
              + (SELECT COUNT(*) FROM ARI.REPAIR_ORDERS r
                 JOIN ARI.REPAIR_EXECUTIONS e ON e.REPAIR_ORDER_ID = r.ID
                 JOIN ARI.REPAIR_CONDITIONS k ON k.REPAIR_ORDER_ID = r.ID
                 WHERE r.DATASET_ID = ? AND k.DOCUMENTED_AT_UTC > e.STARTED_AT_UTC)
              ,(SELECT COUNT(*) FROM ARI.REPAIR_ORDERS r
                JOIN ARI.REPAIR_EXECUTIONS e ON e.REPAIR_ORDER_ID = r.ID
                WHERE r.DATASET_ID = ? AND LENGTH(TRIM(e.REPAIR_REPORT)) = 0)
            FROM DUMMY
            """;
        await using var command = Create(
            connection, sql, datasetId, datasetId, datasetId, datasetId, datasetId, datasetId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new QualityRow(
            Convert.ToInt32(reader.GetValue(0)),
            Convert.ToInt32(reader.GetValue(1)),
            Convert.ToInt32(reader.GetValue(2)),
            Convert.ToInt32(reader.GetValue(3)),
            Convert.ToInt32(reader.GetValue(4)));
    }

    private async Task<List<LaborRow>> ReadLaborRowsAsync(
        DbConnection connection,
        string datasetId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT r.ID, SUM(o.REFERENCE_LABOR_HOURS) * 60, e.ACTUAL_LABOR_MINUTES,
                   CASE WHEN EXISTS (
                       SELECT 1 FROM ARI.REPAIR_CONDITIONS k
                       WHERE k.REPAIR_ORDER_ID = r.ID AND k.CONDITION_CODE = 'VISIBLE_CORROSION'
                   ) THEN 1 ELSE 0 END,
                   CASE WHEN EXISTS (
                       SELECT 1 FROM ARI.REPAIR_COMPLICATIONS c
                       WHERE c.REPAIR_EXECUTION_ID = e.ID
                   ) THEN 1 ELSE 0 END
            FROM ARI.REPAIR_ORDERS r
            JOIN ARI.REPAIR_OPERATIONS o ON o.REPAIR_ORDER_ID = r.ID
            JOIN ARI.REPAIR_EXECUTIONS e ON e.REPAIR_ORDER_ID = r.ID
            WHERE r.DATASET_ID = ?
            GROUP BY r.ID, e.ACTUAL_LABOR_MINUTES, e.ID
            """;
        await using var command = Create(connection, sql, datasetId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<LaborRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new LaborRow(
                Convert.ToDouble(reader.GetDecimal(1)),
                Convert.ToDouble(reader.GetValue(2)),
                Convert.ToInt32(reader.GetValue(3)) == 1,
                Convert.ToInt32(reader.GetValue(4)) == 1));
        }

        return result;
    }

    private async Task<IReadOnlyDictionary<string, int>> ReadComplicationCountsAsync(
        DbConnection connection,
        string datasetId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT c.COMPLICATION_CODE, COUNT(*)
            FROM ARI.REPAIR_COMPLICATIONS c
            JOIN ARI.REPAIR_EXECUTIONS e ON e.ID = c.REPAIR_EXECUTION_ID
            JOIN ARI.REPAIR_ORDERS r ON r.ID = e.REPAIR_ORDER_ID
            WHERE r.DATASET_ID = ?
            GROUP BY c.COMPLICATION_CODE
            ORDER BY c.COMPLICATION_CODE
            """;
        await using var command = Create(connection, sql, datasetId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(reader.GetString(0), Convert.ToInt32(reader.GetValue(1)));
        }

        return result;
    }

    private DbCommand Create(DbConnection connection, string sql, params string[] values)
    {
        var parameters = values.Select(value => (DbType.String, (object?)value)).ToArray();
        return HanaCommand.Create(connection, sql, options.ConnectionTimeoutSeconds, null, parameters);
    }

    private static LaborStatistics CalculateLaborStatistics(IReadOnlyList<LaborRow> rows)
    {
        if (rows.Count == 0)
        {
            throw new InvalidOperationException("Dataset has no complete labor rows.");
        }

        var reference = rows.Select(row => row.ReferenceMinutes).Order().ToArray();
        var actual = rows.Select(row => row.ActualMinutes).Order().ToArray();
        return new LaborStatistics(
            reference.Average(),
            Percentile(reference, 0.5),
            actual.Average(),
            Percentile(actual, 0.5),
            Percentile(actual, 0.9),
            rows.Average(row => Math.Abs(row.ActualMinutes - row.ReferenceMinutes)),
            rows.Average(row =>
                (row.ActualMinutes - row.ReferenceMinutes) / row.ReferenceMinutes * 100));
    }

    private static CorrosionComparison CalculateCorrosionComparison(IReadOnlyList<LaborRow> rows)
    {
        var known = rows.Where(row => row.KnownCorrosion).ToArray();
        var other = rows.Where(row => !row.KnownCorrosion).ToArray();
        return new CorrosionComparison(
            known.Length,
            other.Length,
            known.Average(row => row.ActualMinutes - row.ReferenceMinutes),
            other.Average(row => row.ActualMinutes - row.ReferenceMinutes),
            known.Count(row => row.HasComplication) * 100d / known.Length,
            other.Count(row => row.HasComplication) * 100d / other.Length);
    }

    private static double Percentile(IReadOnlyList<double> sorted, double percentile)
    {
        var position = (sorted.Count - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        return lower == upper
            ? sorted[lower]
            : sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
    }

    private static IReadOnlyList<string> BuildErrors(
        ManifestRow manifest,
        CountRow counts,
        QualityRow quality,
        int laborRows)
    {
        var errors = new List<string>();
        if (manifest.Status != "COMPLETE") errors.Add("Dataset import is not complete.");
        if (manifest.Classification != "SYNTHETIC") errors.Add("Dataset is not labelled synthetic.");
        if (manifest.Expected != manifest.Imported || manifest.Expected != counts.Orders)
            errors.Add("Expected, imported, and stored repair-order counts differ.");
        if (counts.Complete != counts.Orders || laborRows != counts.Orders)
            errors.Add("At least one repair order lacks an operation or execution.");
        if (counts.Operations < counts.Orders) errors.Add("At least one repair has no operation.");
        if (counts.Executions != counts.Orders) errors.Add("Execution cardinality is not one per order.");
        if (quality.Orphans != 0) errors.Add("Orphaned relational records exist.");
        if (quality.InvalidLabor != 0) errors.Add("Invalid labor values exist.");
        if (quality.InvalidVehicles != 0) errors.Add("Implausible vehicle values exist.");
        if (quality.InvalidTimeline != 0) errors.Add("Invalid historical timestamps exist.");
        if (quality.MissingReports != 0) errors.Add("At least one execution has no repair report.");
        return errors;
    }

    private sealed record ManifestRow(int Expected, int Imported, string Classification, string Status);
    private sealed record CountRow(
        int Orders, int Complete, int Operations, int Executions, int Complications, int Conditions);
    private sealed record QualityRow(
        int Orphans,
        int InvalidLabor,
        int InvalidVehicles,
        int InvalidTimeline,
        int MissingReports);
    private sealed record LaborRow(
        double ReferenceMinutes, double ActualMinutes, bool KnownCorrosion, bool HasComplication);
}
