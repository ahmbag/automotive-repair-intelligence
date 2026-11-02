namespace ARI.Core.Contracts;

public sealed record VehicleSnapshotRequest(
    string Manufacturer,
    string Model,
    int ModelYear,
    int MileageKm);

public sealed record CreateEstimateRequest(
    VehicleSnapshotRequest Vehicle,
    string RepairCode,
    string Description,
    IReadOnlyCollection<string> KnownConditions);

public sealed record LaborHourRangeResponse(decimal Lower, decimal Upper);

public sealed record RepairEstimateResponse(
    Guid EstimateId,
    decimal BaselineLaborHours,
    decimal PredictedLaborHours,
    LaborHourRangeResponse? PredictionRangeHours,
    string ModelVersion,
    IReadOnlyCollection<string> RiskFactors,
    string DataSource,
    string Status);
