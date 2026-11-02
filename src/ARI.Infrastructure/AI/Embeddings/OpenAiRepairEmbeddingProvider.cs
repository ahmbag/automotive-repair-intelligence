using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ARI.Core.Abstractions;
using ARI.Core.Contracts;

namespace ARI.Infrastructure.AI.Embeddings;

public sealed class OpenAiRepairEmbeddingProvider(
    HttpClient httpClient,
    EmbeddingProviderOptions options) : IRepairEmbeddingProvider
{
    public async Task<RepairEmbeddingBatch> EmbedAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        Validate(texts);
        var body = JsonContent.Create(new
        {
            model = options.Model,
            input = texts,
            dimensions = options.Dimensions,
            encoding_format = "float"
        });
        var serialized = await body.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var response = await SendWithRetryAsync(serialized, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new RepairEmbeddingUnavailableException(
                $"Embedding provider returned HTTP {(int)response.StatusCode}.");
        }

        try
        {
            using var payload = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = payload.RootElement;
            var vectors = root.GetProperty("data").EnumerateArray()
                .OrderBy(item => item.GetProperty("index").GetInt32())
                .Select(item => (IReadOnlyList<float>)item.GetProperty("embedding")
                    .EnumerateArray().Select(value => value.GetSingle()).ToArray())
                .ToArray();
            if (vectors.Length != texts.Count || vectors.Any(vector =>
                    vector.Count != options.Dimensions || vector.Any(value => !float.IsFinite(value))))
            {
                throw new JsonException("Embedding response count or dimensions were invalid.");
            }

            var model = root.TryGetProperty("model", out var modelElement)
                ? modelElement.GetString() ?? options.Model
                : options.Model;
            var tokens = root.TryGetProperty("usage", out var usage) &&
                         usage.TryGetProperty("prompt_tokens", out var tokenElement)
                ? tokenElement.GetInt32()
                : (int?)null;
            return new RepairEmbeddingBatch(model, options.Dimensions, vectors, tokens);
        }
        catch (JsonException exception)
        {
            throw new RepairEmbeddingUnavailableException(
                "Embedding provider returned an invalid response.", exception);
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
                throw new RepairEmbeddingUnavailableException(
                    $"Embedding request exceeded {options.Timeout.TotalSeconds:0.###} seconds.");
            }
            catch (HttpRequestException exception)
            {
                throw new RepairEmbeddingUnavailableException(
                    "Embedding provider is unreachable.", exception);
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

    private void Validate(IReadOnlyList<string> texts)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new RepairEmbeddingUnavailableException(
                "Embedding API key must be configured through secrets or environment variables.");
        }

        if (string.IsNullOrWhiteSpace(options.Model) || options.Dimensions != 1536)
        {
            throw new RepairEmbeddingUnavailableException(
                "Embedding model must be configured and dimensions must equal 1536 for this schema version.");
        }

        if (options.BatchSize is < 1 or > 2048 || options.MaxRetries is < 0 or > 5)
        {
            throw new RepairEmbeddingUnavailableException("Embedding batch or retry configuration is invalid.");
        }

        if (texts.Count == 0 || texts.Count > options.BatchSize ||
            texts.Any(text => string.IsNullOrWhiteSpace(text)))
        {
            throw new ArgumentException("Embedding input must contain a non-empty configured batch.", nameof(texts));
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.RequestTimeout ||
        statusCode == HttpStatusCode.TooManyRequests ||
        (int)statusCode >= 500;
}

