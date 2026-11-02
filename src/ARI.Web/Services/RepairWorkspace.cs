using ARI.Web.Models;

namespace ARI.Web.Services;

public sealed class RepairWorkspace
{
    public RepairIntake Intake { get; private set; } = DemoCases.Base();
    public WorkflowStatus Status { get; internal set; } = WorkflowStatus.Idle;
    public string? StatusMessage { get; internal set; }
    public RepairAnalysisSnapshot? Baseline { get; internal set; }
    public IReadOnlyList<ScenarioSnapshot> Scenarios => _scenarios.AsReadOnly();
    public event Action? Changed;

    private readonly List<ScenarioSnapshot> _scenarios = [];

    public void SetIntake(RepairIntake intake)
    {
        Intake = intake.Copy();
        Baseline = null;
        _scenarios.Clear();
        Status = WorkflowStatus.Idle;
        StatusMessage = null;
        Notify();
    }

    public void AddScenario(ScenarioSnapshot scenario)
    {
        if (_scenarios.Count >= 5) throw new InvalidOperationException("A maximum of five scenarios is supported.");
        _scenarios.Add(scenario);
        Notify();
    }

    public void Reset()
    {
        Baseline = null;
        _scenarios.Clear();
        Status = WorkflowStatus.Idle;
        StatusMessage = null;
        Notify();
    }

    internal void Notify() => Changed?.Invoke();
}
