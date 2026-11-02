using ARI.Core.Abstractions;
using ARI.Core.Application;
using ARI.Core.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace ARI.Api.Endpoints;

public static class RepairExtractionEndpoints
{
    public static IEndpointRouteBuilder MapRepairExtractionEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/repair-reports/extract", ExtractAsync)
            .WithName("ExtractRepairInformation")
            .WithTags("Repair information")
            .Produces<RepairExtractionResult>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ExtractionValidationProblem>(StatusCodes.Status422UnprocessableEntity)
            .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapPost("/api/estimates/from-description", EstimateFromDescriptionAsync)
            .WithName("EstimateFromDescription")
            .WithTags("Predictions")
            .Produces<EstimateFromDescriptionResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ExtractionValidationProblem>(StatusCodes.Status422UnprocessableEntity)
            .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<IResult> ExtractAsync(
        RepairExtractionRequest request,
        [FromServices] IFeatureExtractor extractor,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await extractor.ExtractAsync(request, cancellationToken)
                .ConfigureAwait(false));
        }
        catch (ArgumentException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (RepairExtractionValidationException exception)
        {
            return Results.Json(
                new ExtractionValidationProblem("ExtractionRejected", exception.Errors),
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (RepairExtractionUnavailableException)
        {
            return Results.Problem(
                "Repair-information extraction is currently unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IResult> EstimateFromDescriptionAsync(
        EstimateFromDescriptionRequest request,
        [FromServices] IFeatureExtractor extractor,
        [FromServices] ILaborPredictionService predictionService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Description) || request.BaselineLaborHours <= 0)
        {
            return Results.Problem(
                "Description and a positive baseline labor duration are required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var extractionRequest = new RepairExtractionRequest(
                request.Description,
                RepairDocumentTypes.PreRepairDescription,
                request.PromptVersion);
            var extraction = await extractor.ExtractAsync(extractionRequest, cancellationToken)
                .ConfigureAwait(false);
            var missing = MissingPredictionFields(extraction);
            if (missing.Count > 0 || !ExtractionQualityPolicy.CanSupplyPredictionFeatures(extraction))
            {
                return Results.Json(
                    new ExtractionValidationProblem(
                        "ManualMappingRequired",
                        missing.Count > 0 ? missing : ["Extraction requires manual review."]),
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var modelYear = extraction.Vehicle.ModelYear!.Value;
            var predictionInput = new LaborPredictionInput(
                extraction.RepairCode!,
                extraction.Vehicle.Manufacturer!,
                extraction.Vehicle.Model!,
                Math.Max(0, DateTimeOffset.UtcNow.Year - modelYear),
                extraction.Vehicle.MileageKm!.Value,
                request.BaselineLaborHours,
                [],
                extraction.KnownBeforeRepair.Select(fact => fact.Code).ToArray());
            var prediction = await predictionService.PredictAsync(predictionInput, cancellationToken)
                .ConfigureAwait(false);

            return Results.Ok(new EstimateFromDescriptionResponse(extraction, prediction));
        }
        catch (ArgumentException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (RepairExtractionValidationException exception)
        {
            return Results.Json(
                new ExtractionValidationProblem("ExtractionRejected", exception.Errors),
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (RepairExtractionUnavailableException)
        {
            return Results.Problem(
                "Repair-information extraction is currently unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (LaborPredictionUnavailableException)
        {
            return Results.Problem(
                "No labor prediction is currently available.",
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

public sealed record EstimateFromDescriptionRequest(
    string Description,
    decimal BaselineLaborHours,
    string PromptVersion = "repair-extraction-v3");

public sealed record EstimateFromDescriptionResponse(
    RepairExtractionResult Extraction,
    LaborPredictionResult Prediction);

public sealed record ExtractionValidationProblem(
    string Status,
    IReadOnlyCollection<string> Errors);
