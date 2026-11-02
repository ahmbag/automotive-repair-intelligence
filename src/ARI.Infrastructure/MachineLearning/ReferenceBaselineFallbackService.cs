using ARI.Core.Abstractions;
using ARI.Core.Contracts;

namespace ARI.Infrastructure.MachineLearning;

/// <summary>
/// Deliberate resilience policy. It never pretends that a baseline is an ML result:
/// method and status make the fallback observable to every caller.
/// </summary>
public sealed class ReferenceBaselineFallbackService(
    PythonLaborPredictionService primary) : ILaborPredictionService
{
    public async Task<LaborPredictionResult> PredictAsync(
        LaborPredictionInput input,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await primary.PredictAsync(input, cancellationToken).ConfigureAwait(false);
        }
        catch (LaborPredictionUnavailableException)
        {
            return new LaborPredictionResult(
                input.BaselineLaborHours,
                "reference-baseline-v1",
                "SYNTHETIC",
                null,
                null,
                "REFERENCE_BASELINE",
                "FALLBACK");
        }
    }
}
