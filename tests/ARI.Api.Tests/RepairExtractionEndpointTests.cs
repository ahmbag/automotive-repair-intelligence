using System.Net;
using System.Net.Http.Json;
using ARI.Api.Endpoints;
using ARI.Core.Abstractions;
using ARI.Core.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ARI.Api.Tests;

public sealed class RepairExtractionEndpointTests
{
    [Fact]
    public async Task Extraction_is_reachable_through_aspnet_core()
    {
        using var factory = Factory(new StubExtractor(Extraction()), new CapturingPredictor());
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/repair-reports/extract",
            new RepairExtractionRequest(
                "Volkswagen Golf VII, model year 2014, 215000 km.",
                RepairDocumentTypes.PreRepairDescription));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<RepairExtractionResult>();
        Assert.Equal("CONTROL_ARM_FRONT_LEFT_REPLACE", result!.RepairCode);
    }

    [Fact]
    public async Task Combined_workflow_passes_only_pre_repair_approved_facts_to_ml()
    {
        var predictor = new CapturingPredictor();
        using var factory = Factory(new StubExtractor(Extraction()), predictor);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/estimates/from-description",
            new EstimateFromDescriptionRequest(
                "Volkswagen Golf VII, model year 2014, 215000 km. Visible corrosion.",
                1.8m));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(predictor.Input);
        Assert.Empty(predictor.Input.KnownConditions);
        Assert.Equal(["VISIBLE_CORROSION"], predictor.Input.ExtractedConditions);
        Assert.DoesNotContain("BROKEN_BOLT", predictor.Input.ExtractedConditions);
    }

    [Fact]
    public async Task Provider_failure_is_a_controlled_503_without_fabricated_result()
    {
        using var factory = Factory(
            new StubExtractor(new RepairExtractionUnavailableException("secret diagnostic")),
            new CapturingPredictor());
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/repair-reports/extract",
            new RepairExtractionRequest("some report", RepairDocumentTypes.RepairCompletionReport));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("secret diagnostic", body);
    }

    private static WebApplicationFactory<Program> Factory(
        IFeatureExtractor extractor,
        ILaborPredictionService predictor) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IFeatureExtractor>();
                services.RemoveAll<ILaborPredictionService>();
                services.AddSingleton(extractor);
                services.AddSingleton(predictor);
            }));

    private static RepairExtractionResult Extraction() => new(
        RepairDocumentTypes.PreRepairDescription,
        new ExtractedVehicle("Volkswagen", "Golf VII", 2014, 215_000),
        "CONTROL_ARM_FRONT_LEFT_REPLACE",
        [
            new ExtractedRepairFact(
                "BROKEN_BOLT",
                RepairInformationTimes.DuringRepair,
                "not allowed into prediction")
        ],
        [
            new ExtractedRepairFact(
                "VISIBLE_CORROSION",
                RepairInformationTimes.BeforeRepair,
                "Visible corrosion")
        ],
        null,
        RepairExtractionStatuses.Extracted,
        new ExtractionTelemetry("repair-extraction-v2", "test", 1, 1, 1, "test"));

    private sealed class StubExtractor : IFeatureExtractor
    {
        private readonly RepairExtractionResult? _result;
        private readonly Exception? _exception;

        public StubExtractor(RepairExtractionResult result) => _result = result;

        public StubExtractor(Exception exception) => _exception = exception;

        public Task<RepairExtractionResult> ExtractAsync(
            RepairExtractionRequest request,
            CancellationToken cancellationToken = default) =>
            _exception is null
                ? Task.FromResult(_result!)
                : Task.FromException<RepairExtractionResult>(_exception);
    }

    private sealed class CapturingPredictor : ILaborPredictionService
    {
        public LaborPredictionInput? Input { get; private set; }

        public Task<LaborPredictionResult> PredictAsync(
            LaborPredictionInput input,
            CancellationToken cancellationToken = default)
        {
            Input = input;
            return Task.FromResult(new LaborPredictionResult(
                2.4m,
                "ari-repair-duration-v1",
                "SYNTHETIC",
                null,
                null,
                "ML_REGRESSION",
                "PREDICTED"));
        }
    }
}
