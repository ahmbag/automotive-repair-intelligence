using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ARI.Core.Abstractions;
using ARI.Core.Contracts;

namespace ARI.Infrastructure.MachineLearning;

/// <summary>
/// Anti-corruption layer around the independently deployed Python inference API.
/// Only this infrastructure class knows its routes and transport semantics.
/// </summary>
public sealed class PythonLaborPredictionService(
    HttpClient httpClient,
    MachineLearningOptions options) : ILaborPredictionService, IMachineLearningReadinessProbe
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<LaborPredictionResult> PredictAsync(
        LaborPredictionInput input,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        try
        {
            using var response = await httpClient
                .PostAsJsonAsync("predict", input, JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                throw new ArgumentException(
                    "The ML service rejected the supplied prediction features.",
                    nameof(input));
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new LaborPredictionUnavailableException(
                    $"ML inference returned HTTP {(int)response.StatusCode}.");
            }

            var result = await response.Content
                .ReadFromJsonAsync<LaborPredictionResult>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            return ValidateResult(result);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new LaborPredictionUnavailableException(
                $"ML inference exceeded the configured timeout of {options.Timeout.TotalSeconds:0.###} seconds.");
        }
        catch (HttpRequestException exception)
        {
            throw new LaborPredictionUnavailableException(
                "ML inference is currently unreachable.",
                exception);
        }
        catch (JsonException exception)
        {
            throw new LaborPredictionUnavailableException(
                "ML inference returned an invalid response.",
                exception);
        }
    }

    public async Task<MachineLearningReadiness> CheckAsync(
        CancellationToken cancellationToken = default)
    {
        if (options.BaseAddress is null)
        {
            return new(false, "NotConfigured", null, null);
        }

        try
        {
            using var response = await httpClient
                .GetAsync("health/ready", cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new(false, "NotReady", null, null);
            }

            var payload = await response.Content
                .ReadFromJsonAsync<ReadinessPayload>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            return payload is null
                ? new(false, "InvalidResponse", null, null)
                : new(true, payload.Status, payload.ModelVersion, payload.FeatureSchemaVersion);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, "NotReady", null, null);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            return new(false, "NotReady", null, null);
        }
    }

    private void EnsureConfigured()
    {
        if (options.BaseAddress is null)
        {
            throw new LaborPredictionUnavailableException(
                "MachineLearning:BaseAddress is not configured.");
        }
    }

    private static LaborPredictionResult ValidateResult(LaborPredictionResult? result)
    {
        if (result is null || result.PredictedLaborHours <= 0m)
        {
            throw new LaborPredictionUnavailableException(
                "ML inference returned a missing or non-positive duration.");
        }

        if (!string.Equals(result.PredictionMethod, "ML_REGRESSION", StringComparison.Ordinal) ||
            !string.Equals(result.Status, "PREDICTED", StringComparison.Ordinal))
        {
            throw new LaborPredictionUnavailableException(
                "ML inference returned unexpected prediction semantics.");
        }

        return result;
    }

    private sealed record ReadinessPayload(
        string Status,
        string? ModelVersion,
        string? FeatureSchemaVersion);
}
