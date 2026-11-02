namespace ARI.Infrastructure.AI;

/// <summary>Secret-free source configuration for the OpenAI Responses API adapter.</summary>
public sealed class LlmProviderOptions
{
    public const string SectionName = "Llm";

    public Uri Endpoint { get; init; } = new("https://api.openai.com/v1/responses");

    public string? Model { get; init; }

    public string? ApiKey { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(45);

    public int MaxRetries { get; init; } = 2;

    public string PromptVersion { get; init; } = RepairExtractionPrompts.DomainAwareVersion;
}
