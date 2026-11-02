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

public sealed class RagEndpointTests
{
    [Fact]
    public async Task Search_endpoint_returns_verifiable_sources()
    {
        var retrieval = new StubRetrieval(Retrieval());
        using var factory = Factory(new StubExtractor(), new StubPredictor(), retrieval, new StubExplainer());
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/repair-cases/search", Query());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<RepairRetrievalResult>();
        Assert.Equal("repair-order:source", Assert.Single(result!.Cases).SourceReference);
    }

    [Fact]
    public async Task Full_workflow_keeps_post_repair_facts_out_of_ml_and_retrieval_query()
    {
        var predictor = new StubPredictor();
        var retrieval = new StubRetrieval(Retrieval());
        using var factory = Factory(new StubExtractor(), predictor, retrieval, new StubExplainer());
        using var client = factory.CreateClient();
        var request = new EvidenceEstimateRequest(
            "Volkswagen Golf VII 2014, 215000 km, front left control arm, visible corrosion.",
            1.8m,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var response = await client.PostAsJsonAsync("/api/estimates/with-evidence", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["VISIBLE_CORROSION"], predictor.Input!.ExtractedConditions);
        Assert.DoesNotContain("BROKEN_BOLT", predictor.Input.ExtractedConditions);
        Assert.Equal(["VISIBLE_CORROSION"], retrieval.Query!.KnownConditions);
        var result = await response.Content.ReadFromJsonAsync<EvidenceEstimateResult>();
        Assert.Equal(2.4m, result!.Prediction.PredictedLaborHours);
        Assert.NotNull(result.Explanation);
    }

    [Fact]
    public async Task Explanation_failure_preserves_prediction_and_retrieved_evidence()
    {
        using var factory = Factory(
            new StubExtractor(),
            new StubPredictor(),
            new StubRetrieval(Retrieval()),
            new StubExplainer(new RepairExplanationUnavailableException("provider down")));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/estimates/with-evidence",
            new EvidenceEstimateRequest("valid description", 1.8m, DateTimeOffset.UtcNow));
        var result = await response.Content.ReadFromJsonAsync<EvidenceEstimateResult>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result!.Prediction);
        Assert.Single(result.Retrieval.Cases);
        Assert.Null(result.Explanation);
        Assert.NotNull(result.ExplanationWarning);
    }

    [Fact]
    public async Task Full_workflow_rejects_incompatible_strategy_and_profile()
    {
        using var factory = Factory(new StubExtractor(), new StubPredictor(),
            new StubRetrieval(Retrieval()), new StubExplainer());
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/estimates/with-evidence",
            new EvidenceEstimateRequest("valid description", 1.8m, DateTimeOffset.UtcNow,
                RepairRetrievalStrategies.StructuredFirstHybrid, 5, 0, "repair-extraction-v3",
                RepairEmbeddingProfiles.ReportV1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static WebApplicationFactory<Program> Factory(
        IFeatureExtractor extractor,
        ILaborPredictionService predictor,
        IRepairRetrievalService retrieval,
        IRepairEvidenceExplainer explainer) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IFeatureExtractor>();
                services.RemoveAll<ILaborPredictionService>();
                services.RemoveAll<IRepairRetrievalService>();
                services.RemoveAll<IRepairEvidenceExplainer>();
                services.AddSingleton(extractor);
                services.AddSingleton(predictor);
                services.AddSingleton(retrieval);
                services.AddSingleton(explainer);
            }));

    private static RepairRetrievalQuery Query() => new(
        "front left control arm with visible corrosion",
        "CONTROL_ARM_FRONT_LEFT_REPLACE",
        "Volkswagen",
        "Golf VII",
        12,
        215_000,
        ["VISIBLE_CORROSION"],
        DateTimeOffset.UtcNow);

    private static RepairRetrievalResult Retrieval() => new(
        RepairRetrievalStrategies.Hybrid,
        "text-embedding-3-small",
        "repair-report-embedding-v1",
        10,
        [new SimilarRepairCase(
            Guid.NewGuid(), 0.9, 0.88, "CONTROL_ARM_FRONT_LEFT_REPLACE",
            "Volkswagen", "Golf VII", 2014, 200_000, 1.8m, 180,
            RepairDocumentTypes.RepairCompletionReport, "SYNTHETIC", "repair-order:source",
            "A seized fastener required additional work.", DateTimeOffset.UtcNow.AddDays(-1))]);

    private sealed class StubExtractor : IFeatureExtractor
    {
        public Task<RepairExtractionResult> ExtractAsync(
            RepairExtractionRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(new RepairExtractionResult(
            RepairDocumentTypes.PreRepairDescription,
            new ExtractedVehicle("Volkswagen", "Golf VII", 2014, 215_000),
            "CONTROL_ARM_FRONT_LEFT_REPLACE",
            [new ExtractedRepairFact("BROKEN_BOLT", RepairInformationTimes.DuringRepair, "historic")],
            [new ExtractedRepairFact("VISIBLE_CORROSION", RepairInformationTimes.BeforeRepair, "visible")],
            null,
            RepairExtractionStatuses.Extracted,
            new ExtractionTelemetry("v2", "test", 1, 1, 1, "request")));
    }

    private sealed class StubPredictor : ILaborPredictionService
    {
        public LaborPredictionInput? Input { get; private set; }

        public Task<LaborPredictionResult> PredictAsync(
            LaborPredictionInput input,
            CancellationToken cancellationToken = default)
        {
            Input = input;
            return Task.FromResult(new LaborPredictionResult(
                2.4m, "ari-repair-duration-v1", "SYNTHETIC", null, null,
                "ML_REGRESSION", "PREDICTED"));
        }
    }

    private sealed class StubRetrieval : IRepairRetrievalService
    {
        private readonly RepairRetrievalResult _result;
        public StubRetrieval(RepairRetrievalResult result) => _result = result;
        public RepairRetrievalQuery? Query { get; private set; }

        public Task<RepairRetrievalResult> SearchAsync(
            RepairRetrievalQuery query,
            CancellationToken cancellationToken = default)
        {
            Query = query;
            return Task.FromResult(_result);
        }
    }

    private sealed class StubExplainer : IRepairEvidenceExplainer
    {
        private readonly Exception? _exception;
        public StubExplainer(Exception? exception = null) => _exception = exception;

        public Task<GroundedRepairExplanation> ExplainAsync(
            LaborPredictionResult prediction,
            RepairRetrievalResult retrieval,
            CancellationToken cancellationToken = default) => _exception is null
            ? Task.FromResult(new GroundedRepairExplanation(
                "Grounded", "MODERATE", false,
                [new GroundedObservation(
                    "Observed historically",
                    "A seized fastener required additional work.",
                    ["repair-order:source"])],
                "grounding-v1", "test", 1, 1, 1))
            : Task.FromException<GroundedRepairExplanation>(_exception);
    }
}
