using ARI.Core.Abstractions;
using ARI.Core.Application;
using ARI.Core.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace ARI.Api.Endpoints;

public static class RagEndpoints
{
    public static IEndpointRouteBuilder MapRagEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/repair-cases/search", SearchAsync)
            .WithName("SearchHistoricalRepairCases")
            .WithTags("Retrieval")
            .Produces<RepairRetrievalResult>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapPost("/api/repair-cases/explain", ExplainAsync)
            .WithName("ExplainHistoricalRepairEvidence")
            .WithTags("Retrieval")
            .Produces<RepairExplanationResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
            .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapPost("/api/estimates/with-evidence", EstimateWithEvidenceAsync)
            .WithName("EstimateWithHistoricalEvidence")
            .WithTags("Predictions")
            .Produces<EvidenceEstimateResult>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
            .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable);
        return endpoints;
    }

    private static async Task<IResult> SearchAsync(
        RepairRetrievalQuery query,
        [FromServices] IRepairRetrievalService retrieval,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await retrieval.SearchAsync(query, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (RepairRetrievalUnavailableException)
        {
            return Results.Problem(
                "Historical repair retrieval is currently unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IResult> ExplainAsync(
        RepairExplanationRequest request,
        [FromServices] IRepairRetrievalService retrieval,
        [FromServices] IRepairEvidenceExplainer explainer,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await retrieval.SearchAsync(request.Query, cancellationToken);
            var explanation = await explainer.ExplainAsync(
                request.Prediction, result, cancellationToken);
            return Results.Ok(new RepairExplanationResponse(result, explanation));
        }
        catch (GroundedExplanationValidationException exception)
        {
            return Results.Json(
                new { status = "GROUNDING_REJECTED", errors = exception.Errors },
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (RepairRetrievalUnavailableException)
        {
            return Results.Problem(
                "Historical repair retrieval is currently unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (RepairExplanationUnavailableException)
        {
            return Results.Problem(
                "Grounded explanation is currently unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IResult> EstimateWithEvidenceAsync(
        EvidenceEstimateRequest request,
        [FromServices] IFeatureExtractor extractor,
        [FromServices] ILaborPredictionService predictionService,
        [FromServices] IRepairRetrievalService retrieval,
        [FromServices] IRepairEvidenceExplainer explainer,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Description) || request.BaselineLaborHours <= 0 ||
            !RepairRetrievalStrategies.IsSupported(request.RetrievalStrategy) ||
            !RepairEmbeddingProfiles.IsSupported(request.EmbeddingProfile) ||
            (RepairRetrievalStrategies.UsesV2(request.RetrievalStrategy) &&
             request.EmbeddingProfile != RepairEmbeddingProfiles.RepairProfileV2))
        {
            return Results.Problem(
                "Description, a positive baseline duration, and a supported retrieval strategy are required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var extraction = await extractor.ExtractAsync(
                new RepairExtractionRequest(
                    request.Description,
                    RepairDocumentTypes.PreRepairDescription,
                    request.PromptVersion),
                cancellationToken);
            var missing = MissingPredictionFields(extraction);
            if (missing.Count > 0 || !ExtractionQualityPolicy.CanSupplyPredictionFeatures(extraction))
            {
                return Results.Json(
                    new { status = "MANUAL_MAPPING_REQUIRED", errors = missing },
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var modelYear = extraction.Vehicle.ModelYear!.Value;
            var safeConditions = extraction.KnownBeforeRepair
                .Where(fact => fact.InformationTime == RepairInformationTimes.BeforeRepair)
                .Select(fact => fact.Code)
                .ToArray();
            var prediction = await predictionService.PredictAsync(
                new LaborPredictionInput(
                    extraction.RepairCode!,
                    extraction.Vehicle.Manufacturer!,
                    extraction.Vehicle.Model!,
                    Math.Max(0, request.AsOfUtc.Year - modelYear),
                    extraction.Vehicle.MileageKm!.Value,
                    request.BaselineLaborHours,
                    [],
                    safeConditions),
                cancellationToken);
            var retrievalResult = await retrieval.SearchAsync(
                new RepairRetrievalQuery(
                    request.Description,
                    extraction.RepairCode!,
                    extraction.Vehicle.Manufacturer,
                    extraction.Vehicle.Model,
                    Math.Max(0, request.AsOfUtc.Year - modelYear),
                    extraction.Vehicle.MileageKm,
                    safeConditions,
                    request.AsOfUtc,
                    request.RetrievalStrategy,
                    request.TopK,
                    request.MinimumSimilarity,
                    request.EmbeddingProfile),
                cancellationToken);

            GroundedRepairExplanation? explanation = null;
            string? warning = null;
            try
            {
                explanation = await explainer.ExplainAsync(
                    prediction, retrievalResult, cancellationToken);
            }
            catch (RepairExplanationUnavailableException)
            {
                warning = "The numerical prediction and retrieved evidence are available, but the generated explanation is not.";
            }

            return Results.Ok(new EvidenceEstimateResult(
                extraction, prediction, retrievalResult, explanation, warning));
        }
        catch (ArgumentException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (RepairExtractionValidationException exception)
        {
            return Results.Json(
                new { status = "EXTRACTION_REJECTED", errors = exception.Errors },
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (GroundedExplanationValidationException exception)
        {
            return Results.Json(
                new { status = "GROUNDING_REJECTED", errors = exception.Errors },
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (Exception exception) when (exception is RepairExtractionUnavailableException
                                          or LaborPredictionUnavailableException
                                          or RepairRetrievalUnavailableException)
        {
            return Results.Problem(
                "The evidence-backed estimate is currently unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static List<string> MissingPredictionFields(RepairExtractionResult result)
    {
        var missing = new List<string>();
        if (result.RepairCode is null) missing.Add("A supported repair code was not extracted.");
        if (result.Vehicle.Manufacturer is null) missing.Add("Vehicle manufacturer was not extracted.");
        if (result.Vehicle.Model is null) missing.Add("Vehicle model was not extracted.");
        if (result.Vehicle.ModelYear is null) missing.Add("Vehicle model year was not extracted.");
        if (result.Vehicle.MileageKm is null) missing.Add("Vehicle mileage was not extracted.");
        return missing;
    }
}

public sealed record RepairExplanationRequest(
    RepairRetrievalQuery Query,
    LaborPredictionResult Prediction);

public sealed record RepairExplanationResponse(
    RepairRetrievalResult Retrieval,
    GroundedRepairExplanation Explanation);

public sealed record EvidenceEstimateRequest(
    string Description,
    decimal BaselineLaborHours,
    DateTimeOffset AsOfUtc,
    string RetrievalStrategy = RepairRetrievalStrategies.RankFusion,
    int TopK = 5,
    double MinimumSimilarity = 0.0,
    string PromptVersion = "repair-extraction-v3",
    string EmbeddingProfile = RepairEmbeddingProfiles.RepairProfileV2);
