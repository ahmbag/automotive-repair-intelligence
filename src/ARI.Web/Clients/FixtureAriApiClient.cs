using ARI.Core.Contracts;

namespace ARI.Web.Clients;

/// <summary>
/// Deterministic browser-test adapter. It is enabled only through AriApi:UseFixture and every UI
/// page displays a TEST DATA banner while active. Live acceptance never uses this class.
/// </summary>
public sealed class FixtureAriApiClient : IAriApiClient
{
    public bool IsFixture => true;

    public async Task<ApiResult<EvidenceEstimateResult>> EstimateWithEvidenceAsync(
        EvidenceEstimateRequestDto request, string correlationId, CancellationToken cancellationToken)
    {
        await Task.Delay(120, cancellationToken);
        if (request.Description.Contains("SIMULATE_BACKEND_OUTAGE", StringComparison.OrdinalIgnoreCase))
            throw new AriApiException("ARI.Api cannot be reached. Check the API and try again.", 503, "API_UNAVAILABLE");
        if (request.Description.Contains("somewhere underneath", StringComparison.OrdinalIgnoreCase))
            throw new AriApiException("The description needs manual mapping before prediction can continue.",
                422, "MANUAL_MAPPING_REQUIRED", ["Vehicle manufacturer was not extracted.", "A supported repair code was not extracted."]);

        var corrosion = request.Description.Contains("corrosion", StringComparison.OrdinalIgnoreCase);
        var mileage = request.Description.Contains("318000", StringComparison.Ordinal) ? 318000 : 128000;
        var extraction = new RepairExtractionResult(
            RepairDocumentTypes.PreRepairDescription,
            new ExtractedVehicle("Volkswagen", "Golf", 2018, mileage),
            "CONTROL_ARM_FRONT_LEFT_REPLACE",
            [],
            corrosion
                ? [new ExtractedRepairFact("VISIBLE_CORROSION", RepairInformationTimes.BeforeRepair,
                    "Visible surface corrosion is present around the left front control-arm mounting area.")]
                : [],
            null,
            RepairExtractionStatuses.Accepted,
            new ExtractionTelemetry("repair-extraction-v3", "fixture-llm", 42, 180, 48, "fixture-request"));
        var predicted = 2.487136m + (mileage - 128000) / 1_000_000m + (corrosion ? .25m : 0m);
        var prediction = new LaborPredictionResult(predicted, "ridge-v1", "SYNTHETIC_HISTORICAL_REPAIRS",
            null, null, "RIDGE_REGRESSION", "PREDICTED");
        var cases = Enumerable.Range(1, 5).Select(index => new SimilarRepairCase(
            Guid.Parse($"00000000-0000-0000-0000-{index:000000000000}"),
            1.0 - index * .08, .98 - index * .07,
            "CONTROL_ARM_FRONT_LEFT_REPLACE", "Volkswagen", index % 2 == 0 ? "Passat" : "Golf",
            2016 + index % 3, 105000 + index * 12000, 2.2m,
            135 + index * 9, "SYNTHETIC_REPAIR_ORDER", "VALIDATED_COMPLETION_REPORT",
            $"ARI-SYN-{index:000}",
            $"Control arm replacement completed; documented actual labor was {135 + index * 9} minutes.",
            DateTimeOffset.UtcNow.AddYears(-index))).ToArray();
        var retrieval = new RepairRetrievalResult(
            RepairRetrievalStrategies.RankFusion, "text-embedding-3-small", "1", 55, cases,
            RepairEmbeddingProfiles.RepairProfileV2, 14, false, null);
        var explanation = new GroundedRepairExplanation(
            "The numerical estimate is supported by five comparable synthetic historical repairs.",
            "MODERATE", false,
            cases.Select(item => new GroundedObservation(
                $"A comparable {item.Manufacturer} {item.Model} repair required {item.ActualLaborMinutes} minutes.",
                item.SourceExcerpt, [item.SourceReference])).ToArray(),
            "repair-grounding-v2", "fixture-llm", 38, 260, 90);
        var result = new EvidenceEstimateResult(extraction, prediction, retrieval, explanation, null);
        return new(result, correlationId, TimeSpan.FromMilliseconds(255));
    }

    public async Task<ApiResult<LaborPredictionResult>> PredictAsync(
        LaborPredictionInput request, string correlationId, CancellationToken cancellationToken)
    {
        await Task.Delay(90, cancellationToken);
        if (request.MileageKm is < 0 or > 5_000_000 || request.VehicleAgeYears is < 0 or > 100)
            throw new AriApiException("The scenario values are outside their valid ranges.", 400, "INVALID_SCENARIO");
        var corrosion = request.ExtractedConditions.Contains("VISIBLE_CORROSION") ? .25m : 0m;
        var value = 2.05m + request.MileageKm / 1_000_000m + request.VehicleAgeYears * .018m + corrosion;
        return new(new LaborPredictionResult(value, "ridge-v1", "SYNTHETIC_HISTORICAL_REPAIRS",
            null, null, "RIDGE_REGRESSION", "PREDICTED"), correlationId, TimeSpan.FromMilliseconds(90));
    }

    public Task<SystemHealthSnapshot> GetHealthAsync(CancellationToken cancellationToken) => Task.FromResult(
        new SystemHealthSnapshot(ProbeState.Online, ProbeState.Online, ProbeState.Online,
            ProbeState.Unknown, ProbeState.Unknown, DateTimeOffset.UtcNow));
}
