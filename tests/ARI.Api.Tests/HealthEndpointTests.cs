using System.Text.Json;
using ARI.Api.Endpoints;
using ARI.Infrastructure.Persistence.Hana;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ARI.Api.Tests;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task Health_endpoint_returns_http_200_and_healthy_payload()
    {
        var builder = CreateTestBuilder();
        await using var app = builder.Build();
        app.MapFoundationEndpoints();

        var (statusCode, document) = await InvokeEndpointAsync(app, "/health");
        using (document)
        {
            Assert.Equal(StatusCodes.Status200OK, statusCode);
            Assert.Equal("Healthy", document.RootElement.GetProperty("status").GetString());
            Assert.Equal("ARI.Api", document.RootElement.GetProperty("service").GetString());
        }
    }

    [Fact]
    public async Task Readiness_endpoint_returns_http_200_for_authenticated_hana_connection()
    {
        var builder = CreateTestBuilder();
        builder.Services.AddSingleton<IHanaConnectivityProbe>(
            new StubConnectivityProbe(new HanaConnectivityResult("ARI_APP")));
        await using var app = builder.Build();
        app.MapFoundationEndpoints();

        var (statusCode, document) = await InvokeEndpointAsync(app, "/health/ready");
        using (document)
        {
            Assert.Equal(StatusCodes.Status200OK, statusCode);
            Assert.Equal("Ready", document.RootElement.GetProperty("status").GetString());
            Assert.Equal(
                "ARI_APP",
                document.RootElement.GetProperty("authenticatedUser").GetString());
        }
    }

    [Fact]
    public async Task Readiness_endpoint_returns_http_503_when_hana_is_unavailable()
    {
        var builder = CreateTestBuilder();
        builder.Services.AddSingleton<IHanaConnectivityProbe>(
            new StubConnectivityProbe(new InvalidOperationException("Unavailable")));
        await using var app = builder.Build();
        app.MapFoundationEndpoints();

        var (statusCode, document) = await InvokeEndpointAsync(app, "/health/ready");
        using (document)
        {
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusCode);
            Assert.Equal("NotReady", document.RootElement.GetProperty("status").GetString());
            Assert.Equal(
                JsonValueKind.Null,
                document.RootElement.GetProperty("authenticatedUser").ValueKind);
        }
    }

    private static async Task<(int StatusCode, JsonDocument Document)> InvokeEndpointAsync(
        WebApplication app,
        string route)
    {
        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(candidate => candidate.RoutePattern.RawText == route);

        var context = new DefaultHttpContext
        {
            RequestServices = app.Services,
            Response = { Body = new MemoryStream() }
        };

        await endpoint.RequestDelegate!(context);
        context.Response.Body.Position = 0;
        var document = await JsonDocument.ParseAsync(context.Response.Body);

        return (context.Response.StatusCode, document);
    }

    private static WebApplicationBuilder CreateTestBuilder()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        return builder;
    }

    private sealed class StubConnectivityProbe : IHanaConnectivityProbe
    {
        private readonly HanaConnectivityResult? _result;
        private readonly Exception? _exception;

        public StubConnectivityProbe(HanaConnectivityResult result)
        {
            _result = result;
        }

        public StubConnectivityProbe(Exception exception)
        {
            _exception = exception;
        }

        public Task<HanaConnectivityResult> CheckAsync(
            CancellationToken cancellationToken = default) =>
            _exception is null
                ? Task.FromResult(_result!)
                : Task.FromException<HanaConnectivityResult>(_exception);
    }
}
