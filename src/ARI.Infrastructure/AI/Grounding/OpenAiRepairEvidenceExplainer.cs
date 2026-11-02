using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ARI.Core.Abstractions;
using ARI.Core.Application;
using ARI.Core.Contracts;
using ARI.Infrastructure.AI;

namespace ARI.Infrastructure.AI.Grounding;

public sealed class OpenAiRepairEvidenceExplainer(
    HttpClient httpClient,
    LlmProviderOptions options) : IRepairEvidenceExplainer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public async Task<GroundedRepairExplanation> ExplainAsync(
        LaborPredictionResult prediction,
        RepairRetrievalResult retrieval,
        CancellationToken cancellationToken = default)
    {
        if (retrieval.Cases.Count == 0)
        {
            return new GroundedRepairExplanation(
                "No sufficiently comparable historical evidence was retrieved.",
                "NONE",
                true,
                [],
                GroundingPrompts.Version,
                options.Model ?? "not-called",
                0,
                0,
                0);
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey) || string.IsNullOrWhiteSpace(options.Model))
        {
            throw new RepairExplanationUnavailableException(
                "LLM API key and model must be configured through secrets or environment variables.");
        }

        var sourcePayload = retrieval.Cases.Select(item => new
        {
            item.SourceReference,
            item.RepairCode,
            item.Manufacturer,
            item.Model,
            item.MileageKm,
            item.ActualLaborMinutes,
            item.SourceExcerpt
        });
        var input = JsonSerializer.Serialize(new
        {
            mlPrediction = new { prediction.PredictedLaborHours, prediction.ModelVersion },
            sources = sourcePayload
        }, JsonOptions);
        var body = JsonContent.Create(new
        {
            model = options.Model,
            instructions = GroundingPrompts.SystemPrompt,
            input = $"BEGIN_UNTRUSTED_RETRIEVAL_DATA\n{input}\nEND_UNTRUSTED_RETRIEVAL_DATA",
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = "ari_grounded_repair_explanation",
                    strict = true,
                    schema = Schema()
                }
            }
        }, options: JsonOptions);
        var serialized = await body.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var stopwatch = Stopwatch.StartNew();
        using var response = await SendWithRetryAsync(serialized, cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();
        if (!response.IsSuccessStatusCode)
        {
            throw new RepairExplanationUnavailableException(
                $"Explanation provider returned HTTP {(int)response.StatusCode}.");
        }

        try
        {
            using var payload = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = payload.RootElement;
            if (root.GetProperty("status").GetString() != "completed")
            {
                throw new RepairExplanationUnavailableException("Explanation response did not complete.");
            }

            var provider = JsonSerializer.Deserialize<ProviderExplanation>(FindOutputText(root), JsonOptions)
                ?? throw new JsonException("Explanation output was empty.");
            var explanation = new GroundedRepairExplanation(
                provider.Summary,
                provider.EvidenceStrength,
                provider.InsufficientEvidence,
                provider.Observations.Select(observation => new GroundedObservation(
                    observation.Statement, observation.EvidenceQuote, observation.SourceReferences)).ToArray(),
                GroundingPrompts.Version,
                options.Model,
                stopwatch.ElapsedMilliseconds,
                ReadTokens(root, "input_tokens"),
                ReadTokens(root, "output_tokens"));
            var errors = GroundedExplanationValidator.Validate(explanation, retrieval);
            if (errors.Count > 0)
            {
                throw new GroundedExplanationValidationException(errors);
            }

            return explanation;
        }
        catch (GroundedExplanationValidationException)
        {
            throw;
        }
        catch (RepairExplanationUnavailableException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException)
        {
            throw new RepairExplanationUnavailableException(
                "Explanation provider returned invalid structured output.", exception);
        }
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        string body,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, options.Endpoint)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
            request.Headers.Add("X-Client-Request-Id", Guid.NewGuid().ToString("D"));
            HttpResponseMessage response;
            try
            {
                response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new RepairExplanationUnavailableException("Explanation provider timed out.");
            }
            catch (HttpRequestException exception)
            {
                throw new RepairExplanationUnavailableException(
                    "Explanation provider is unreachable.", exception);
            }

            if (!IsTransient(response.StatusCode) || attempt >= options.MaxRetries)
            {
                return response;
            }

            response.Dispose();
            await Task.Delay(TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempt)), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static object Schema() => new
    {
        type = "object",
        additionalProperties = false,
        required = new[] { "summary", "evidenceStrength", "insufficientEvidence", "observations" },
        properties = new
        {
            summary = new { type = "string" },
            evidenceStrength = new { type = "string", @enum = new[] { "NONE", "LIMITED", "MODERATE", "STRONG" } },
            insufficientEvidence = new { type = "boolean" },
            observations = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "statement", "evidenceQuote", "sourceReferences" },
                    properties = new
                    {
                        statement = new { type = "string" },
                        evidenceQuote = new { type = "string" },
                        sourceReferences = new { type = "array", items = new { type = "string" } }
                    }
                }
            }
        }
    };

    private static string FindOutputText(JsonElement root)
    {
        foreach (var output in root.GetProperty("output").EnumerateArray())
        foreach (var part in output.TryGetProperty("content", out var content)
                     ? content.EnumerateArray()
                     : [])
        {
            if (part.TryGetProperty("type", out var type) && type.GetString() == "output_text")
            {
                return part.GetProperty("text").GetString() ?? throw new JsonException();
            }
        }

        throw new JsonException("No explanation output text was returned.");
    }

    private static int? ReadTokens(JsonElement root, string name) =>
        root.TryGetProperty("usage", out var usage) && usage.TryGetProperty(name, out var count)
            ? count.GetInt32()
            : null;

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.RequestTimeout ||
        statusCode == HttpStatusCode.TooManyRequests ||
        (int)statusCode >= 500;

    private sealed record ProviderExplanation(
        string Summary,
        string EvidenceStrength,
        bool InsufficientEvidence,
        IReadOnlyCollection<ProviderObservation> Observations);

    private sealed record ProviderObservation(
        string Statement,
        string EvidenceQuote,
        IReadOnlyCollection<string> SourceReferences);
}
