using ARI.Infrastructure.Persistence.Hana;
using Microsoft.AspNetCore.Mvc;

namespace ARI.Api.Endpoints;

public static class FoundationEndpoints
{
    public static IEndpointRouteBuilder MapFoundationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/health",
                () => Results.Ok(new HealthResponse("Healthy", "ARI.Api")))
            .WithName("Health")
            .WithTags("Operations")
            .Produces<HealthResponse>(StatusCodes.Status200OK);

        endpoints.MapGet("/health/ready", CheckHanaReadinessAsync)
            .WithName("Readiness")
            .WithTags("Operations")
            .Produces<ReadinessResponse>(StatusCodes.Status200OK)
            .Produces<ReadinessResponse>(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<IResult> CheckHanaReadinessAsync(
        [FromServices] IHanaConnectivityProbe connectivityProbe,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await connectivityProbe
                .CheckAsync(cancellationToken)
                .ConfigureAwait(false);

            return Results.Ok(
                new ReadinessResponse("Ready", "SAP HANA", result.AuthenticatedUser));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HanaConnectivityException exception)
        {
            var logger = loggerFactory.CreateLogger("ARI.Api.HanaReadiness");
            logger.LogWarning(
                "SAP HANA readiness check failed (native error {NativeError}, SQL state {SqlState}): {SafeDiagnostic}",
                exception.NativeError,
                exception.SqlState,
                exception.SafeDiagnostic);

            return Results.Json(
                new ReadinessResponse("NotReady", "SAP HANA", null),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception exception)
        {
            var logger = loggerFactory.CreateLogger("ARI.Api.HanaReadiness");
            logger.LogWarning(
                "SAP HANA readiness check failed ({ExceptionType}).",
                exception.GetType().Name);

            return Results.Json(
                new ReadinessResponse("NotReady", "SAP HANA", null),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}

public sealed record HealthResponse(string Status, string Service);

public sealed record ReadinessResponse(
    string Status,
    string Dependency,
    string? AuthenticatedUser);
