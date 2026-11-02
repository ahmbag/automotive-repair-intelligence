using ARI.Web.Clients;
using ARI.Web.Models;
using ARI.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ARI.Web.Tests;

public sealed class WorkflowTests
{
    [Fact]
    public async Task FullWorkflow_CommitsFiveEvidenceCasesAndTrace()
    {
        var (workspace, coordinator) = Create();
        Assert.True(await coordinator.AnalyzeAsync(DemoCases.Base()));
        Assert.Equal(WorkflowStatus.Completed, workspace.Status);
        Assert.Equal(5, workspace.Baseline!.Result.Retrieval.Cases.Count);
        Assert.StartsWith("ui-", workspace.Baseline.CorrelationId);
        Assert.Equal("repair-extraction-v3", workspace.Baseline.Result.Extraction.Telemetry.PromptVersion);
    }

    [Fact]
    public async Task ManualMapping_DoesNotCommitPrediction()
    {
        var (workspace, coordinator) = Create();
        var ambiguous = DemoCases.All.Single(item => item.Name == "Ambiguous Description").Create();
        Assert.False(await coordinator.AnalyzeAsync(ambiguous));
        Assert.Equal(WorkflowStatus.ManualReviewRequired, workspace.Status);
        Assert.Null(workspace.Baseline);
    }

    [Fact]
    public async Task TwoScenarios_AreNewSnapshotsAndNeverMutateBaseline()
    {
        var (workspace, coordinator) = Create();
        await coordinator.AnalyzeAsync(DemoCases.Base());
        var original = workspace.Baseline!.BaselineInput;
        await coordinator.RunScenarioAsync(new ScenarioDraft
            { Name = "High mileage", MileageKm = 240000, VehicleAgeYears = 8, RepairCode = original.RepairCode });
        await coordinator.RunScenarioAsync(new ScenarioDraft
            { Name = "Corrosion", MileageKm = 128000, VehicleAgeYears = 8, RepairCode = original.RepairCode, VisibleCorrosion = true });
        Assert.Equal(2, workspace.Scenarios.Count);
        Assert.Equal(original, workspace.Baseline.BaselineInput);
        Assert.NotEqual(workspace.Scenarios[0].Prediction.PredictedLaborHours, workspace.Scenarios[1].Prediction.PredictedLaborHours);
    }

    [Fact]
    public async Task RapidSecondRequest_PreventsStaleFirstCommit()
    {
        var (workspace, coordinator) = Create();
        var first = DemoCases.Base();
        var second = DemoCases.Base(); second.MileageKm = 318000; second.Description = second.Description.Replace("128000", "318000");
        var firstTask = coordinator.AnalyzeAsync(first);
        await Task.Delay(10);
        var secondTask = coordinator.AnalyzeAsync(second);
        await Task.WhenAll(firstTask, secondTask);
        Assert.Equal(318000, workspace.Baseline!.Result.Extraction.Vehicle.MileageKm);
    }

    [Fact]
    public async Task CancelledRequest_CommitsNoResult()
    {
        var (workspace, coordinator) = Create();
        var task = coordinator.AnalyzeAsync(DemoCases.Base());
        coordinator.CancelActive();
        await task;
        Assert.Null(workspace.Baseline);
        Assert.Equal(WorkflowStatus.Cancelled, workspace.Status);
    }

    private static (RepairWorkspace, RepairAnalysisCoordinator) Create()
    {
        var workspace = new RepairWorkspace();
        var client = new FixtureAriApiClient();
        var coordinator = new RepairAnalysisCoordinator(client, workspace,
            NullLogger<RepairAnalysisCoordinator>.Instance);
        return (workspace, coordinator);
    }
}
