namespace ARI.Infrastructure.MachineLearning;

/// <summary>Settings for the REST boundary to the independently deployed Python service.</summary>
public sealed class MachineLearningOptions
{
    public const string SectionName = "MachineLearning";

    public Uri? BaseAddress { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Disabled by default so an unavailable model is visible. Enable deliberately
    /// when the business process prefers the reference time over an HTTP 503.
    /// </summary>
    public bool UseReferenceBaselineFallback { get; init; }
}
