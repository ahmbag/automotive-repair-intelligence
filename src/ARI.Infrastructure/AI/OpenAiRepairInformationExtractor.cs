using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ARI.Core.Abstractions;
using ARI.Core.Application;
using ARI.Core.Contracts;
using ARI.Infrastructure.AI.Classification;
using Microsoft.Extensions.Logging;

namespace ARI.Infrastructure.AI;

/// <summary>
/// OpenAI Responses API adapter using strict Structured Outputs. No document or generated content
/// is logged; operational logs contain only model/prompt identifiers, latency, usage, and request ID.
/// </summary>
public sealed class OpenAiRepairInformationExtractor(
    HttpClient httpClient,
    LlmProviderOptions options,
    ILogger<OpenAiRepairInformationExtractor> logger) : IFeatureExtractor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public async Task<RepairExtractionResult> ExtractAsync(
        RepairExtractionRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateConfigurationAndRequest(request);
        var prompt = RepairExtractionPrompts.Get(request.PromptVersion);
        var stopwatch = Stopwatch.StartNew();
        using var response = await SendWithRetryAsync(
            CreateProviderRequest(request, prompt), cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        var responseId = response.Headers.TryGetValues("x-request-id", out var requestIds)
            ? requestIds.FirstOrDefault()
            : null;
        if (!response.IsSuccessStatusCode)
        {
            throw new RepairExtractionUnavailableException(
                $"LLM provider returned HTTP {(int)response.StatusCode}.");
        }

        try
        {
            using var payload = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = payload.RootElement;
            if (!string.Equals(root.GetProperty("status").GetString(), "completed", StringComparison.Ordinal))
            {
                throw new RepairExtractionUnavailableException("LLM response did not complete.");
            }

            var jsonText = FindOutputText(root);
            var extracted = JsonSerializer.Deserialize<ProviderExtraction>(jsonText, JsonOptions)
                ?? throw new JsonException("Structured output was empty.");
            ValidateProviderShape(extracted);
            var result = Map(
                extracted,
                request.PromptVersion,
                options.Model!,
                stopwatch.ElapsedMilliseconds,
                ReadTokenCount(root, "input_tokens"),
                ReadTokenCount(root, "output_tokens"),
                responseId);
            // The provider status is a transport hint only. Quality release belongs to the
            // application policy, so domain validation starts from a neutral valid state.
            result = result with { Status = RepairExtractionStatuses.Accepted };
            if (request.PromptVersion == RepairExtractionPrompts.QualityVersion)
            {
                result = result with
                {
                    ObservedComplications = request.DocumentType == RepairDocumentTypes.RepairCompletionReport
                        ? RepairComplicationClassifier.Classify(request.Document)
                        : []
                };
            }

            var validation = RepairExtractionValidator.Validate(
                request, result, DateTimeOffset.UtcNow.Year);
            result = result with
            {
                Status = ExtractionQualityPolicy.Determine(request, result, validation)
            };
            validation = RepairExtractionValidator.Validate(
                request, result, DateTimeOffset.UtcNow.Year);
            if (!validation.IsValid)
            {
                throw new RepairExtractionValidationException(validation.Errors);
            }

            logger.LogInformation(
                "LLM extraction completed with model {ModelId}, prompt {PromptVersion}, latency {LatencyMs} ms, input tokens {InputTokens}, output tokens {OutputTokens}, request {RequestId}.",
                result.Telemetry.ModelId,
                result.Telemetry.PromptVersion,
                result.Telemetry.LatencyMilliseconds,
                result.Telemetry.InputTokens,
                result.Telemetry.OutputTokens,
                result.Telemetry.ProviderRequestId);
            return result;
        }
        catch (RepairExtractionUnavailableException)
        {
            throw;
        }
        catch (RepairExtractionValidationException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new RepairExtractionValidationException(
                [$"Provider output failed strict schema deserialization: {exception.Message}"]);
        }
        catch (Exception exception) when (
            exception is KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new RepairExtractionValidationException(
                [$"Provider response envelope was invalid: {exception.Message}"]);
        }
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpRequestMessage initialRequest,
        CancellationToken cancellationToken)
    {
        using (initialRequest)
        {
            var body = await initialRequest.Content!.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            for (var attempt = 0; ; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, options.Endpoint)
                {
                    Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
                request.Headers.Add("X-Client-Request-Id", Guid.NewGuid().ToString());

                HttpResponseMessage response;
                try
                {
                    response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new RepairExtractionUnavailableException(
                        $"LLM request exceeded the configured timeout of {options.Timeout.TotalSeconds:0.###} seconds.");
                }
                catch (HttpRequestException exception)
                {
                    throw new RepairExtractionUnavailableException(
                        "LLM provider is currently unreachable.", exception);
                }

                if (!IsTransient(response.StatusCode) || attempt >= options.MaxRetries)
                {
                    return response;
                }

                response.Dispose();
                var delay = TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempt));
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private HttpRequestMessage CreateProviderRequest(
        RepairExtractionRequest request,
        string prompt)
    {
        var providerBody = new
        {
            model = options.Model,
            instructions = prompt,
            input = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "input_text",
                            text = $"APPLICATION_DOCUMENT_TYPE: {request.DocumentType}\nBEGIN_UNTRUSTED_DOCUMENT\n{request.Document}\nEND_UNTRUSTED_DOCUMENT"
                        }
                    }
                }
            },
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = "ari_repair_information_extraction",
                    strict = true,
                    schema = RepairExtractionJsonSchema.Create()
                }
            }
        };
        return new HttpRequestMessage(HttpMethod.Post, options.Endpoint)
        {
            Content = JsonContent.Create(providerBody, options: JsonOptions)
        };
    }

    private void ValidateConfigurationAndRequest(RepairExtractionRequest request)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey) || string.IsNullOrWhiteSpace(options.Model))
        {
            throw new RepairExtractionUnavailableException(
                "LLM API key and model must be configured through secrets or environment variables.");
        }

        if (options.MaxRetries is < 0 or > 5)
        {
            throw new RepairExtractionUnavailableException("LLM retry count must be between 0 and 5.");
        }

        if (string.IsNullOrWhiteSpace(request.Document) || request.Document.Length > 20_000)
        {
            throw new ArgumentException("Document is required and limited to 20,000 characters.", nameof(request));
        }

        if (!RepairDocumentTypes.IsSupported(request.DocumentType))
        {
            throw new ArgumentException("Document type is unsupported.", nameof(request));
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.RequestTimeout ||
        statusCode == HttpStatusCode.TooManyRequests ||
        (int)statusCode >= 500;

    private static string FindOutputText(JsonElement root)
    {
        foreach (var output in root.GetProperty("output").EnumerateArray())
        {
            if (!output.TryGetProperty("content", out var content))
            {
                continue;
            }

            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("type", out var type) &&
                    type.GetString() == "output_text" &&
                    part.TryGetProperty("text", out var text))
                {
                    return text.GetString() ?? throw new JsonException("Output text was null.");
                }

                if (part.TryGetProperty("type", out type) && type.GetString() == "refusal")
                {
                    throw new RepairExtractionUnavailableException("LLM provider refused the document.");
                }
            }
        }

        throw new JsonException("Response contained no structured output text.");
    }

    private static int? ReadTokenCount(JsonElement root, string name) =>
        root.TryGetProperty("usage", out var usage) &&
        usage.TryGetProperty(name, out var count)
            ? count.GetInt32()
            : null;

    private static RepairExtractionResult Map(
        ProviderExtraction source,
        string promptVersion,
        string model,
        long latencyMilliseconds,
        int? inputTokens,
        int? outputTokens,
        string? providerRequestId) => new(
            source.DocumentType,
            new ExtractedVehicle(
                source.Vehicle.Manufacturer,
                source.Vehicle.Model,
                source.Vehicle.ModelYear,
                source.Vehicle.MileageKm),
            source.RepairCode,
            source.ObservedComplications.Select(MapFact).ToArray(),
            source.KnownBeforeRepair.Select(MapFact).ToArray(),
            source.ActualLaborMinutes,
            source.Status,
            new ExtractionTelemetry(
                promptVersion,
                model,
                latencyMilliseconds,
                inputTokens,
                outputTokens,
                providerRequestId));

    private static void ValidateProviderShape(ProviderExtraction source)
    {
        if (string.IsNullOrWhiteSpace(source.DocumentType) ||
            source.Vehicle is null ||
            source.ObservedComplications is null ||
            source.KnownBeforeRepair is null ||
            string.IsNullOrWhiteSpace(source.Status))
        {
            throw new JsonException("Structured output omitted a required field.");
        }

        if (source.ObservedComplications.Concat(source.KnownBeforeRepair).Any(fact =>
                string.IsNullOrWhiteSpace(fact.Code) ||
                string.IsNullOrWhiteSpace(fact.InformationTime) ||
                string.IsNullOrWhiteSpace(fact.Evidence)))
        {
            throw new JsonException("Structured output contained an incomplete fact.");
        }
    }

    private static ExtractedRepairFact MapFact(ProviderFact fact) =>
        new(fact.Code, fact.InformationTime, fact.Evidence);

    private sealed record ProviderExtraction(
        string DocumentType,
        ProviderVehicle Vehicle,
        string? RepairCode,
        IReadOnlyCollection<ProviderFact> ObservedComplications,
        IReadOnlyCollection<ProviderFact> KnownBeforeRepair,
        int? ActualLaborMinutes,
        string Status);

    private sealed record ProviderVehicle(
        string? Manufacturer,
        string? Model,
        int? ModelYear,
        int? MileageKm);

    private sealed record ProviderFact(string Code, string InformationTime, string Evidence);
}
