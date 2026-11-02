using ARI.Core.Domain;

namespace ARI.Core.Contracts;

public sealed record HistoricalDatasetManifest(
    string DatasetId,
    string GeneratorVersion,
    int Seed,
    int RecordCount,
    string DataClassification,
    int SchemaVersion,
    string Checksum);

public sealed record HistoricalRepairDataset(
    HistoricalDatasetManifest Manifest,
    IReadOnlyList<HistoricalRepairCase> Records);

public sealed record HistoricalImportOptions(int BatchSize = 100);

public sealed record HistoricalImportResult(
    string DatasetId,
    int ImportedCount,
    int SkippedCount,
    bool AlreadyComplete);

public interface IHistoricalRepairImportService
{
    Task<HistoricalImportResult> ImportAsync(
        HistoricalRepairDataset dataset,
        HistoricalImportOptions options,
        CancellationToken cancellationToken = default);
}
