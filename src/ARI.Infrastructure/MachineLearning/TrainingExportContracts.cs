namespace ARI.Infrastructure.MachineLearning;

public sealed record TrainingExportManifest(
    string DatasetId,
    string GeneratorVersion,
    string SourceDatasetChecksum,
    int RecordCount,
    string DataClassification,
    int ExportSchemaVersion,
    string FeatureSchemaVersion,
    string ExportChecksum);

public sealed record TrainingExportRow(
    Guid RepairOrderId,
    Guid RepairOperationId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset CompletedAtUtc,
    int VehicleAgeYears,
    int MileageKm,
    string Manufacturer,
    string Model,
    string RepairCode,
    int BaselineLaborMinutes,
    bool KnownCorrosion,
    int ActualLaborMinutes,
    IReadOnlyList<string> ComplicationCodes,
    string RepairReport);

public sealed record TrainingExport(
    TrainingExportManifest Manifest,
    IReadOnlyList<TrainingExportRow> Records);
