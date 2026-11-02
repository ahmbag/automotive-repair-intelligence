namespace ARI.Infrastructure.MachineLearning;

public interface IMachineLearningReadinessProbe
{
    Task<MachineLearningReadiness> CheckAsync(CancellationToken cancellationToken = default);
}

public sealed record MachineLearningReadiness(
    bool IsReady,
    string Status,
    string? ModelVersion,
    string? FeatureSchemaVersion);
