using ARI.Core.Application;
using ARI.Core.Contracts;

namespace ARI.Core.Tests;

public sealed class GroundedExplanationValidatorTests
{
    [Fact]
    public void Retrieved_source_reference_is_accepted()
    {
        var retrieval = Retrieval("repair-order:one");
        var explanation = Explanation(
            false,
            [new GroundedObservation("A prior repair recorded additional work.", "Evidence", ["repair-order:one"])]);

        Assert.Empty(GroundedExplanationValidator.Validate(explanation, retrieval));
    }

    [Fact]
    public void Invented_source_reference_is_rejected()
    {
        var errors = GroundedExplanationValidator.Validate(
            Explanation(false, [new GroundedObservation("Claim", "Evidence", ["repair-order:invented"])]),
            Retrieval("repair-order:real"));

        Assert.Contains(errors, error => error.Contains("Unknown source reference"));
    }

    [Fact]
    public void Insufficient_evidence_cannot_contain_observations()
    {
        var errors = GroundedExplanationValidator.Validate(
            Explanation(true, [new GroundedObservation("Claim", "Evidence", ["repair-order:one"])]),
            Retrieval("repair-order:one"));

        Assert.Contains(errors, error => error.Contains("cannot contain observations"));
    }

    private static RepairRetrievalResult Retrieval(string reference) => new(
        RepairRetrievalStrategies.Hybrid,
        "embedding-model",
        "v1",
        1,
        [new SimilarRepairCase(
            Guid.NewGuid(), 0.9, 0.9, "REPAIR", "Maker", "Model", 2015, 100_000,
            1.8m, 120, RepairDocumentTypes.RepairCompletionReport, "SYNTHETIC",
            reference, "Evidence", DateTimeOffset.UtcNow.AddDays(-1))]);

    private static GroundedRepairExplanation Explanation(
        bool insufficient,
        IReadOnlyCollection<GroundedObservation> observations) => new(
            "Summary", insufficient ? "NONE" : "MODERATE", insufficient, observations,
            "prompt-v1", "model", 1, 1, 1);
}
