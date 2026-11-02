using ARI.Core.Contracts;

namespace ARI.Core.Abstractions;

public interface IRepairEmbeddingProvider
{
    Task<RepairEmbeddingBatch> EmbedAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default);
}

public interface IRepairRetrievalService
{
    Task<RepairRetrievalResult> SearchAsync(
        RepairRetrievalQuery query,
        CancellationToken cancellationToken = default);
}

public interface IRepairEvidenceExplainer
{
    Task<GroundedRepairExplanation> ExplainAsync(
        LaborPredictionResult prediction,
        RepairRetrievalResult retrieval,
        CancellationToken cancellationToken = default);
}

public sealed class RepairEmbeddingUnavailableException : Exception
{
    public RepairEmbeddingUnavailableException(string message) : base(message) { }
    public RepairEmbeddingUnavailableException(string message, Exception inner) : base(message, inner) { }
}

public sealed class RepairRetrievalUnavailableException : Exception
{
    public RepairRetrievalUnavailableException(string message) : base(message) { }
    public RepairRetrievalUnavailableException(string message, Exception inner) : base(message, inner) { }
}

public sealed class RepairExplanationUnavailableException : Exception
{
    public RepairExplanationUnavailableException(string message) : base(message) { }
    public RepairExplanationUnavailableException(string message, Exception inner) : base(message, inner) { }
}

public sealed class GroundedExplanationValidationException(
    IReadOnlyCollection<string> errors) : Exception("Grounded explanation validation failed.")
{
    public IReadOnlyCollection<string> Errors { get; } = errors;
}

