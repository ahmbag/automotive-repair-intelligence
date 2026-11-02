using System.Net;
using System.Text;
using ARI.Web.Clients;
using Microsoft.Extensions.Logging.Abstractions;

namespace ARI.Web.Tests;

public sealed class ApiClientTests
{
    [Fact]
    public async Task NumericProblemDetailsStatus_IsTranslatedWithoutLeakingBody()
    {
        var handler = new StubHandler(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent(
                """{"type":"about:blank","title":"Service Unavailable","status":503,"detail":"Controlled failure."}""",
                Encoding.UTF8, "application/problem+json")
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new LiveAriApiClient(http, NullLogger<LiveAriApiClient>.Instance);

        var exception = await Assert.ThrowsAsync<AriApiException>(() =>
            client.EstimateWithEvidenceAsync(
                new("description", 2m, DateTimeOffset.UtcNow), "test-correlation", default));

        Assert.Equal(503, exception.StatusCode);
        Assert.Equal("Controlled failure.", exception.Message);
    }

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(response);
    }
}
