namespace ARI.Infrastructure.AI.Embeddings;

public sealed class EmbeddingProviderOptions
{
    public const string SectionName = "Embeddings";

    public Uri Endpoint { get; init; } = new("https://api.openai.com/v1/embeddings");
    public string Model { get; init; } = "text-embedding-3-small";
    public string ModelVersion { get; init; } = "repair-report-embedding-v1";
    public string V2ModelVersion { get; init; } = "repair-search-embedding-v2";
    public string V2ProfileVersion { get; init; } = "canonical-repair-search-profile-v2";
    public string V2DocumentVersion { get; init; } = "2.0.0";
    public string DefaultRetrievalStrategy { get; init; } = "RANK_FUSION_V2";
    public string? ApiKey { get; init; }
    public int Dimensions { get; init; } = 1536;
    public int BatchSize { get; init; } = 100;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);
    public int MaxRetries { get; init; } = 2;
}
