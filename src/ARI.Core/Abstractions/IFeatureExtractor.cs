using ARI.Core.Contracts;

namespace ARI.Core.Abstractions;

/// <summary>
/// Extracts evidence-backed facts. It must never predict labor time or expose provider concepts.
/// </summary>
public interface IFeatureExtractor
{
    Task<RepairExtractionResult> ExtractAsync(
        RepairExtractionRequest request,
        CancellationToken cancellationToken = default);
}
