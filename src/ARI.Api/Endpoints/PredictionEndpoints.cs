using ARI.Core.Abstractions;
using ARI.Core.Contracts;
using ARI.Infrastructure.MachineLearning;
using Microsoft.AspNetCore.Mvc;

namespace ARI.Api.Endpoints;

public static class PredictionEndpoints
{
    public static IEndpointRouteBuilder MapPredictionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/predictions", PredictAsync)
            .WithName("PredictLabor")
            .WithTags("Predictions")
            .Produces<LaborPredictionResult>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapGet("/health/ml/ready", CheckReadinessAsync)
            .WithName("MachineLearningReadiness")
            .WithTags("Operations")
            .Produces<MachineLearningReadiness>(StatusCodes.Status200OK)
            .Produces<MachineLearningReadiness>(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<IResult> PredictAsync(
        LaborPredictionInput input,
        [FromServices] ILaborPredictionService predictionService,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(input);
        if (validationError is not null)
        {
            return Results.Problem(validationError, statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var result = await predictionService
                .PredictAsync(input, cancellationToken)
                .ConfigureAwait(false);
            return Results.Ok(result);
        }
        catch (ArgumentException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (LaborPredictionUnavailableException)
        {
            return Results.Problem(
                "No labor prediction is currently available.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static string? Validate(LaborPredictionInput input)
    {
        if (string.IsNullOrWhiteSpace(input.RepairCode) ||
            string.IsNullOrWhiteSpace(input.Manufacturer) ||
            string.IsNullOrWhiteSpace(input.Model))
        {
            return "Repair code, manufacturer, and model are required.";
        }

        if (input.VehicleAgeYears is < 0 or > 100 ||
            input.MileageKm is < 0 or > 5_000_000 ||
            input.BaselineLaborHours <= 0m)
        {
            return "The numerical prediction features are outside their valid ranges.";
        }

        return null;
    }

    private static async Task<IResult> CheckReadinessAsync(
        [FromServices] IMachineLearningReadinessProbe readinessProbe,
        CancellationToken cancellationToken)
    {
        var result = await readinessProbe.CheckAsync(cancellationToken).ConfigureAwait(false);
        return result.IsReady
            ? Results.Ok(result)
            : Results.Json(result, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}
