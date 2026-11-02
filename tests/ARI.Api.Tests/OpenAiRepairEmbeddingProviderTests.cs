using System.Net;
using System.Text;
using System.Text.Json;
using ARI.Core.Abstractions;
using ARI.Infrastructure.AI.Embeddings;

namespace ARI.Api.Tests;

public sealed class OpenAiRepairEmbeddingProviderTests
{
    [Fact]
    public async Task Configured_model_dimensions_and_bearer_auth_are_sent()
    {
        string? requestBody = null;
        string? auth = null;
        using var client = Client(async request =>
        {
            requestBody = await request.Content!.ReadAsStringAsync();
            auth = request.Headers.Authorization?.Scheme;
            return Response(1536);
        });
        var provider = Provider(client);

        var result = await provider.EmbedAsync(["repair report"]);

        using var body = JsonDocument.Parse(requestBody!);
        Assert.Equal("text-embedding-3-small", body.RootElement.GetProperty("model").GetString());
        Assert.Equal(1536, body.RootElement.GetProperty("dimensions").GetInt32());
        Assert.Equal("Bearer", auth);
        Assert.Equal(1536, Assert.Single(result.Vectors).Count);
    }

    [Fact]
    public async Task Wrong_vector_dimension_is_rejected_without_storage_use()
    {
        using var client = Client(_ => Task.FromResult(Response(3)));

        await Assert.ThrowsAsync<RepairEmbeddingUnavailableException>(
            () => Provider(client).EmbedAsync(["repair report"]));
    }

    [Fact]
    public async Task Rate_limit_retry_is_bounded()
    {
        var calls = 0;
        using var client = Client(_ => Task.FromResult(
            ++calls == 1
                ? new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                : Response(1536)));

        await Provider(client, retries: 1).EmbedAsync(["repair report"]);

        Assert.Equal(2, calls);
    }

    private static OpenAiRepairEmbeddingProvider Provider(HttpClient client, int retries = 0) => new(
        client,
        new EmbeddingProviderOptions
        {
            ApiKey = "test-secret",
            Model = "text-embedding-3-small",
            Dimensions = 1536,
            BatchSize = 100,
            MaxRetries = retries,
            Timeout = client.Timeout
        });

    private static HttpClient Client(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) =>
        new(new StubHandler(handler)) { Timeout = TimeSpan.FromSeconds(5) };

    private static HttpResponseMessage Response(int dimensions)
    {
        var vector = Enumerable.Repeat(0.01f, dimensions).ToArray();
        var json = JsonSerializer.Serialize(new
        {
            data = new[] { new { index = 0, embedding = vector } },
            model = "text-embedding-3-small",
            usage = new { prompt_tokens = 4, total_tokens = 4 }
        });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => handler(request);
    }
}

