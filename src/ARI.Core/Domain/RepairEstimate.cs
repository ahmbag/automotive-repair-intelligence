namespace ARI.Core.Domain;

/// <summary>An auditable estimate produced only from information available at prediction time.</summary>
public sealed record RepairEstimate(
    Guid Id,
    Guid RepairOrderId,
    decimal BaselineLaborHours,
    decimal PredictedLaborHours,
    PredictionRange? PredictionRangeHours,
    string ModelVersion,
    IReadOnlyCollection<string> RiskFactors,
    EstimateDataSource DataSource,
    EstimateStatus Status,
    DateTimeOffset CreatedAt,
    string InputDataVersion,
    string CalculationStrategy);

public sealed record PredictionRange(decimal Lower, decimal Upper);

public enum EstimateDataSource
{
    Synthetic,
    Historical,
    Baseline
}

public enum EstimateStatus
{
    Estimated,
    BaselineFallback,
    Failed
}
