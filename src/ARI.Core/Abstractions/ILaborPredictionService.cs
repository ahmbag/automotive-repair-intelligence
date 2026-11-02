using ARI.Core.Contracts;

namespace ARI.Core.Abstractions;

/// <summary>Produces numerical labor estimates from a versioned ML model.</summary>
public interface ILaborPredictionService
{
    Task<LaborPredictionResult> PredictAsync(
        LaborPredictionInput input,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Signals that no numerical prediction can currently be produced. The exception is
/// provider-neutral: ARI.Core neither knows about Python nor about HTTP.
/// </summary>
public sealed class LaborPredictionUnavailableException : Exception
{
    public LaborPredictionUnavailableException(string message)
        : base(message)
    {
    }

    public LaborPredictionUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
