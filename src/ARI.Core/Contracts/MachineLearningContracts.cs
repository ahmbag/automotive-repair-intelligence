namespace ARI.Core.Contracts;

/// <summary>Input for the versioned numerical model; it contains no post-repair facts.</summary>
public sealed record LaborPredictionInput(
    string RepairCode,
    string Manufacturer,
    string Model,
    int VehicleAgeYears,
    int MileageKm,
    decimal BaselineLaborHours,
    IReadOnlyCollection<string> KnownConditions,
    IReadOnlyCollection<string> ExtractedConditions);

public sealed record LaborPredictionResult(
    decimal PredictedLaborHours,
    string ModelVersion,
    string DataSource,
    decimal? RangeLowerHours,
    decimal? RangeUpperHours,
    string PredictionMethod,
    string Status);
