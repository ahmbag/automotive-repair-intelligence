using ARI.Core.Contracts;
using ARI.Web.Clients;
using ARI.Web.Models;

namespace ARI.Web.Services;

public sealed class RepairAnalysisCoordinator(
    IAriApiClient client,
    RepairWorkspace workspace,
    ILogger<RepairAnalysisCoordinator> logger) : IDisposable
{
    private CancellationTokenSource? _activeRequest;
    private long _generation;
    public bool IsFixture => client.IsFixture;

    public async Task<bool> AnalyzeAsync(RepairIntake intake, CancellationToken cancellationToken = default)
    {
        CancelActive();
        var generation = Interlocked.Increment(ref _generation);
        var generationId = Guid.NewGuid();
        var correlationId = $"ui-{generationId:N}";
        _activeRequest = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        workspace.Status = WorkflowStatus.Validating;
        workspace.StatusMessage = "Validating trusted input and domain ranges…";
        workspace.Notify();

        // EditForm runs the same DataAnnotations/IValidatableObject validation before this point.
        workspace.Status = WorkflowStatus.Running;
        workspace.StatusMessage = "Analysis is running. The API is performing extraction, prediction and retrieval…";
        workspace.Notify();

        try
        {
            var response = await client.EstimateWithEvidenceAsync(
                new EvidenceEstimateRequestDto(
                    intake.BuildAnalysisDocument(), intake.BaselineLaborHours,
                    new DateTimeOffset(DateTime.SpecifyKind(intake.AsOfDate.Date, DateTimeKind.Utc))),
                correlationId, _activeRequest.Token);
            if (generation != Volatile.Read(ref _generation)) return false;

            var extraction = response.Value.Extraction;
            var baselineInput = new LaborPredictionInput(
                extraction.RepairCode!, extraction.Vehicle.Manufacturer!, extraction.Vehicle.Model!,
                Math.Max(0, intake.AsOfDate.Year - extraction.Vehicle.ModelYear!.Value),
                extraction.Vehicle.MileageKm!.Value, intake.BaselineLaborHours,
                intake.VisiblePriorDamage ? ["VISIBLE_PRIOR_DAMAGE"] : [],
                extraction.KnownBeforeRepair.Select(fact => fact.Code).ToArray());
            workspace.Baseline = new RepairAnalysisSnapshot(
                generationId, response.CorrelationId, DateTimeOffset.UtcNow, response.Duration,
                intake.Copy(), response.Value, baselineInput);
            workspace.Status = response.Value.Explanation is null
                ? WorkflowStatus.PartialResult : extraction.Status switch
                {
                    RepairExtractionStatuses.Partial => WorkflowStatus.PartialResult,
                    RepairExtractionStatuses.NeedsReview => WorkflowStatus.ManualReviewRequired,
                    _ => WorkflowStatus.Completed
                };
            workspace.StatusMessage = workspace.Status == WorkflowStatus.Completed
                ? "Analysis completed. Prediction and evidence are ready."
                : "A partial result is available. Review the warning before using it.";
            workspace.Notify();
            return true;
        }
        catch (OperationCanceledException)
        {
            if (generation == Volatile.Read(ref _generation))
            {
                workspace.Status = WorkflowStatus.Cancelled;
                workspace.StatusMessage = "Analysis cancelled. No result was committed.";
                workspace.Notify();
            }
            return false;
        }
        catch (AriApiException exception)
        {
            if (generation != Volatile.Read(ref _generation)) return false;
            workspace.Status = exception.StatusCode == 422
                ? WorkflowStatus.ManualReviewRequired : WorkflowStatus.Failed;
            workspace.StatusMessage = exception.Details.Count == 0
                ? exception.Message : $"{exception.Message} {string.Join(" ", exception.Details)}";
            workspace.Notify();
            logger.LogWarning("ARI workflow failed with {ErrorCode}, HTTP {StatusCode}, correlation {CorrelationId}",
                exception.ErrorCode, exception.StatusCode, correlationId);
            return false;
        }
        finally
        {
            if (generation == Volatile.Read(ref _generation))
            {
                _activeRequest?.Dispose();
                _activeRequest = null;
            }
        }
    }

    public async Task<ScenarioSnapshot> RunScenarioAsync(ScenarioDraft draft, CancellationToken cancellationToken = default)
    {
        var baseline = workspace.Baseline ?? throw new InvalidOperationException("Complete a baseline analysis first.");
        if (workspace.Scenarios.Count >= 5) throw new InvalidOperationException("A maximum of five scenarios is supported.");
        if (draft.MileageKm is < 0 or > 5_000_000 || draft.VehicleAgeYears is < 0 or > 100 ||
            !RepairCatalog.Codes.Contains(draft.RepairCode))
            throw new ArgumentException("Scenario values are outside the supported pre-repair feature ranges.");

        var conditions = baseline.BaselineInput.ExtractedConditions
            .Where(item => item != "VISIBLE_CORROSION").ToList();
        if (draft.VisibleCorrosion) conditions.Add("VISIBLE_CORROSION");
        var input = baseline.BaselineInput with
        {
            RepairCode = draft.RepairCode,
            MileageKm = draft.MileageKm,
            VehicleAgeYears = draft.VehicleAgeYears,
            ExtractedConditions = conditions.AsReadOnly()
        };
        var correlationId = $"scenario-{Guid.NewGuid():N}";

        ApiResult<LaborPredictionResult> prediction;
        if (draft.FullEvidenceRefresh)
        {
            var description = $"{baseline.Intake.BuildAnalysisDocument()}\nHypothetical scenario: mileage {draft.MileageKm} km; vehicle age {draft.VehicleAgeYears} years; repair code {draft.RepairCode}; visible corrosion {draft.VisibleCorrosion}.";
            var full = await client.EstimateWithEvidenceAsync(new EvidenceEstimateRequestDto(
                description, input.BaselineLaborHours,
                new DateTimeOffset(DateTime.SpecifyKind(baseline.Intake.AsOfDate, DateTimeKind.Utc))),
                correlationId, cancellationToken);
            prediction = new(full.Value.Prediction, full.CorrelationId, full.Duration);
        }
        else
        {
            prediction = await client.PredictAsync(input, correlationId, cancellationToken);
        }

        var scenario = new ScenarioSnapshot(Guid.NewGuid(),
            string.IsNullOrWhiteSpace(draft.Name) ? $"Scenario {workspace.Scenarios.Count + 1}" : draft.Name.Trim(),
            DateTimeOffset.UtcNow, input, prediction.Value, prediction.CorrelationId,
            prediction.Duration, draft.FullEvidenceRefresh);
        workspace.AddScenario(scenario);
        logger.LogInformation("Scenario {ScenarioId} completed in {ElapsedMs} ms with correlation {CorrelationId}",
            scenario.ScenarioId, scenario.Duration.TotalMilliseconds, scenario.CorrelationId);
        return scenario;
    }

    public void CancelActive()
    {
        _activeRequest?.Cancel();
    }

    public void Dispose()
    {
        _activeRequest?.Cancel();
        _activeRequest?.Dispose();
    }
}
