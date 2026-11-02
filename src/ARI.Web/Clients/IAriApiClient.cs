using ARI.Core.Contracts;

namespace ARI.Web.Clients;

public interface IAriApiClient
{
    bool IsFixture { get; }
    Task<ApiResult<EvidenceEstimateResult>> EstimateWithEvidenceAsync(
        EvidenceEstimateRequestDto request, string correlationId, CancellationToken cancellationToken);
    Task<ApiResult<LaborPredictionResult>> PredictAsync(
        LaborPredictionInput request, string correlationId, CancellationToken cancellationToken);
    Task<SystemHealthSnapshot> GetHealthAsync(CancellationToken cancellationToken);
}

public sealed record EvidenceEstimateRequestDto(
    string Description,
    decimal BaselineLaborHours,
    DateTimeOffset AsOfUtc,
    string RetrievalStrategy = RepairRetrievalStrategies.RankFusion,
    int TopK = 5,
    double MinimumSimilarity = 0.0,
    string PromptVersion = "repair-extraction-v3",
    string EmbeddingProfile = RepairEmbeddingProfiles.RepairProfileV2);

public sealed record ApiResult<T>(T Value, string CorrelationId, TimeSpan Duration);

public enum ProbeState { Online, Offline, Unknown }

public sealed record SystemHealthSnapshot(
    ProbeState Api,
    ProbeState Hana,
    ProbeState MachineLearning,
    ProbeState Llm,
    ProbeState Embeddings,
    DateTimeOffset CheckedAtUtc)
{
    public static SystemHealthSnapshot Unavailable => new(
        ProbeState.Offline, ProbeState.Unknown, ProbeState.Unknown,
        ProbeState.Unknown, ProbeState.Unknown, DateTimeOffset.UtcNow);
}

public sealed class AriApiException(
    string message,
    int? statusCode = null,
    string? errorCode = null,
    IReadOnlyList<string>? details = null,
    Exception? innerException = null) : Exception(message, innerException)
{
    public int? StatusCode { get; } = statusCode;
    public string? ErrorCode { get; } = errorCode;
    public IReadOnlyList<string> Details { get; } = details ?? [];
}
