using ARI.Core.Contracts;

namespace ARI.Web.Models;

public enum WorkflowStatus
{
    Idle, Validating, Running, Completed, PartialResult,
    ManualReviewRequired, Failed, Cancelled
}

public sealed record RepairAnalysisSnapshot(
    Guid GenerationId,
    string CorrelationId,
    DateTimeOffset CompletedAtUtc,
    TimeSpan TotalDuration,
    RepairIntake Intake,
    EvidenceEstimateResult Result,
    LaborPredictionInput BaselineInput);

public sealed record ScenarioSnapshot(
    Guid ScenarioId,
    string Name,
    DateTimeOffset CreatedAtUtc,
    LaborPredictionInput Input,
    LaborPredictionResult Prediction,
    string CorrelationId,
    TimeSpan Duration,
    bool FullEvidenceRefresh = false)
{
    public decimal DeltaHours(decimal baseline) => Prediction.PredictedLaborHours - baseline;
    public decimal DeltaPercent(decimal baseline) => baseline == 0 ? 0 : DeltaHours(baseline) / baseline * 100m;
}

public sealed class ScenarioDraft
{
    public string Name { get; set; } = "Scenario";
    public int MileageKm { get; set; }
    public int VehicleAgeYears { get; set; }
    public string RepairCode { get; set; } = RepairCatalog.Codes[0];
    public bool VisibleCorrosion { get; set; }
    public bool FullEvidenceRefresh { get; set; }
}
