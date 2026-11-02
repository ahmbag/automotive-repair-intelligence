using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ARI.Core.Contracts;

namespace ARI.Web.Clients;

public sealed class LiveAriApiClient(HttpClient httpClient, ILogger<LiveAriApiClient> logger) : IAriApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public bool IsFixture => false;

    public Task<ApiResult<EvidenceEstimateResult>> EstimateWithEvidenceAsync(
        EvidenceEstimateRequestDto request, string correlationId, CancellationToken cancellationToken) =>
        PostAsync<EvidenceEstimateRequestDto, EvidenceEstimateResult>(
            "/api/estimates/with-evidence", request, correlationId, cancellationToken);

    public Task<ApiResult<LaborPredictionResult>> PredictAsync(
        LaborPredictionInput request, string correlationId, CancellationToken cancellationToken) =>
        PostAsync<LaborPredictionInput, LaborPredictionResult>(
            "/api/predictions", request, correlationId, cancellationToken);

    public async Task<SystemHealthSnapshot> GetHealthAsync(CancellationToken cancellationToken)
    {
        var api = await ProbeAsync("/health", cancellationToken);
        if (api == ProbeState.Offline) return SystemHealthSnapshot.Unavailable;
        var hana = await ProbeAsync("/health/ready", cancellationToken);
        var ml = await ProbeAsync("/health/ml/ready", cancellationToken);
        // No dedicated provider probes exist. Unknown is deliberate and truthful.
        return new(api, hana, ml, ProbeState.Unknown, ProbeState.Unknown, DateTimeOffset.UtcNow);
    }

    private async Task<ApiResult<TResponse>> PostAsync<TRequest, TResponse>(
        string path, TRequest request, string correlationId, CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        using var message = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        message.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);

        try
        {
            using var response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode) throw TranslateFailure(response.StatusCode, body);

            var value = JsonSerializer.Deserialize<TResponse>(body, JsonOptions)
                ?? throw new AriApiException("The API returned an empty response.");
            var returnedId = response.Headers.TryGetValues("X-Correlation-ID", out var values)
                ? values.FirstOrDefault() ?? correlationId : correlationId;
            logger.LogInformation("ARI API {Path} completed in {ElapsedMs} ms with correlation {CorrelationId}",
                path, timer.ElapsedMilliseconds, returnedId);
            return new(value, returnedId, timer.Elapsed);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AriApiException("The analysis timed out. You can retry safely.", 504, "TIMEOUT");
        }
        catch (HttpRequestException exception)
        {
            throw new AriApiException("ARI.Api cannot be reached. Check the API and try again.",
                503, "API_UNAVAILABLE", innerException: exception);
        }
        catch (JsonException exception)
        {
            throw new AriApiException("ARI.Api returned an unreadable response.",
                502, "INVALID_RESPONSE", innerException: exception);
        }
    }

    private async Task<ProbeState> ProbeAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(path, cancellationToken);
            return response.IsSuccessStatusCode ? ProbeState.Online : ProbeState.Offline;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return ProbeState.Offline;
        }
    }

    private static AriApiException TranslateFailure(HttpStatusCode status, string body)
    {
        string? code = null;
        string message = status switch
        {
            HttpStatusCode.UnprocessableEntity => "The description needs manual mapping before prediction can continue.",
            HttpStatusCode.TooManyRequests => "The AI provider is rate-limited. Wait briefly, then retry.",
            HttpStatusCode.ServiceUnavailable => "A required backend service is unavailable. Review system status and retry.",
            HttpStatusCode.BadRequest => "The API rejected one or more analysis values.",
            _ => "The analysis request failed in a controlled way."
        };
        var details = new List<string>();
        try
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("status", out var statusElement) &&
                statusElement.ValueKind == JsonValueKind.String)
                code = statusElement.GetString();
            if (json.RootElement.TryGetProperty("title", out var title) &&
                title.ValueKind == JsonValueKind.String)
                message = title.GetString() ?? message;
            if (json.RootElement.TryGetProperty("detail", out var detail) &&
                detail.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(detail.GetString()))
                message = detail.GetString()!;
            if (json.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
                details.AddRange(errors.EnumerateArray().Select(item => item.GetString()).OfType<string>());
        }
        catch (JsonException) { }
        return new AriApiException(message, (int)status, code, details);
    }
}
