using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ARI.Core.Abstractions;
using ARI.Core.Contracts;
using ARI.Infrastructure.MachineLearning;

namespace ARI.Api.Tests;

public sealed class MachineLearningAdapterTests
{
    [Fact]
    public async Task Adapter_maps_core_contract_to_python_and_validates_response()
    {
        LaborPredictionInput? captured = null;
        using var client = CreateClient(async request =>
        {
            captured = await request.Content!.ReadFromJsonAsync<LaborPredictionInput>();
            return JsonResponse(HttpStatusCode.OK, new
            {
                predictedLaborHours = 2.25m,
                modelVersion = "ari-repair-duration-v1",
                dataSource = "SYNTHETIC",
                rangeLowerHours = (decimal?)null,
                rangeUpperHours = (decimal?)null,
                predictionMethod = "ML_REGRESSION",
                status = "PREDICTED"
            });
        });
        var service = CreateService(client);

        var result = await service.PredictAsync(ValidInput());

        Assert.NotNull(captured);
        Assert.Equal("CONTROL_ARM_FRONT_LEFT_REPLACE", captured.RepairCode);
        Assert.Equal(2.25m, result.PredictedLaborHours);
        Assert.Equal("ari-repair-duration-v1", result.ModelVersion);
        Assert.Equal("ML_REGRESSION", result.PredictionMethod);
        Assert.Equal("PREDICTED", result.Status);
    }

    [Fact]
    public async Task Adapter_translates_unavailable_service_to_provider_neutral_exception()
    {
        using var client = CreateClient(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var service = CreateService(client);

        await Assert.ThrowsAsync<LaborPredictionUnavailableException>(
            () => service.PredictAsync(ValidInput()));
    }

    [Fact]
    public async Task Adapter_translates_http_timeout_to_provider_neutral_exception()
    {
        using var client = new HttpClient(new TimeoutHandler())
        {
            BaseAddress = Options().BaseAddress,
            Timeout = TimeSpan.FromMilliseconds(25)
        };
        var service = new PythonLaborPredictionService(
            client,
            new MachineLearningOptions
            {
                BaseAddress = Options().BaseAddress,
                Timeout = client.Timeout
            });

        var exception = await Assert.ThrowsAsync<LaborPredictionUnavailableException>(
            () => service.PredictAsync(ValidInput()));

        Assert.Contains("timeout", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Adapter_rejects_non_positive_model_output()
    {
        using var client = CreateClient(_ => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            new
            {
                predictedLaborHours = -1m,
                modelVersion = "broken",
                dataSource = "SYNTHETIC",
                rangeLowerHours = (decimal?)null,
                rangeUpperHours = (decimal?)null,
                predictionMethod = "ML_REGRESSION",
                status = "PREDICTED"
            })));
        var service = CreateService(client);

        await Assert.ThrowsAsync<LaborPredictionUnavailableException>(
            () => service.PredictAsync(ValidInput()));
    }

    [Fact]
    public async Task Explicit_fallback_is_labelled_and_uses_reference_hours()
    {
        using var client = CreateClient(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var primary = CreateService(client);
        var service = new ReferenceBaselineFallbackService(primary);

        var result = await service.PredictAsync(ValidInput());

        Assert.Equal(1.8m, result.PredictedLaborHours);
        Assert.Equal("REFERENCE_BASELINE", result.PredictionMethod);
        Assert.Equal("FALLBACK", result.Status);
        Assert.Equal("reference-baseline-v1", result.ModelVersion);
    }

    [Fact]
    public async Task Readiness_exposes_loaded_model_version()
    {
        using var client = CreateClient(_ => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            new
            {
                status = "Ready",
                modelVersion = "ari-repair-duration-v1",
                featureSchemaVersion = "ari-labor-features-v1"
            })));
        var service = CreateService(client);

        var readiness = await service.CheckAsync();

        Assert.True(readiness.IsReady);
        Assert.Equal("ari-repair-duration-v1", readiness.ModelVersion);
        Assert.Equal("ari-labor-features-v1", readiness.FeatureSchemaVersion);
    }

    private static PythonLaborPredictionService CreateService(HttpClient client) =>
        new(client, Options());

    private static MachineLearningOptions Options() => new()
    {
        BaseAddress = new Uri("http://localhost:8100/"),
        Timeout = TimeSpan.FromSeconds(2)
    };

    private static HttpClient CreateClient(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) =>
        new(new StubHandler(responder))
        {
            BaseAddress = Options().BaseAddress,
            Timeout = Options().Timeout
        };

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, object body) =>
        new(status)
        {
            Content = JsonContent.Create(body, options: new JsonSerializerOptions(JsonSerializerDefaults.Web))
        };

    private static LaborPredictionInput ValidInput() => new(
        "CONTROL_ARM_FRONT_LEFT_REPLACE",
        "Volkswagen",
        "Golf VII",
        12,
        215_000,
        1.8m,
        ["VISIBLE_CORROSION"],
        []);

    private sealed class StubHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => responder(request);
    }

    private sealed class TimeoutHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The cancelled delay must not complete.");
        }
    }
}
