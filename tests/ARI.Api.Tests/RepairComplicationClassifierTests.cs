using ARI.Infrastructure.AI.Classification;

namespace ARI.Api.Tests;

public sealed class RepairComplicationClassifierTests
{
    public static TheoryData<string, string> SupportedPhrases => new()
    {
        { "Corroded mounting hardware required additional extraction work.", "SEIZED_FASTENER" },
        { "One or more fasteners resisted removal and needed careful release.", "SEIZED_FASTENER" },
        { "Dismantling took longer because mounting hardware was seized.", "SEIZED_FASTENER" },
        { "A mounting bolt broke during removal and was replaced.", "BROKEN_BOLT" },
        { "Bolt extraction and replacement were necessary during dismantling.", "BROKEN_BOLT" },
        { "A failed fastener added an unplanned removal step.", "BROKEN_BOLT" },
        { "The bushing seat required cleaning and corrective preparation.", "DAMAGED_BUSHING_SEAT" },
        { "Additional work was carried out around the worn bushing mounting area.", "DAMAGED_BUSHING_SEAT" },
        { "The mounting seat needed attention before the new component could be installed.", "DAMAGED_BUSHING_SEAT" },
        { "Final alignment required an additional correction.", "ALIGNMENT_CORRECTION" },
        { "A follow-up adjustment was made during the geometry check.", "ALIGNMENT_CORRECTION" },
        { "Wheel geometry needed further correction after assembly.", "ALIGNMENT_CORRECTION" }
    };

    [Theory]
    [MemberData(nameof(SupportedPhrases))]
    public void Every_document_supported_phrase_maps_to_its_taxonomy_code(
        string text,
        string expectedCode)
    {
        var result = RepairComplicationClassifier.Classify(text);

        var fact = Assert.Single(result);
        Assert.Equal(expectedCode, fact.Code);
        Assert.Equal(text, fact.Evidence);
    }

    [Theory]
    [InlineData("No mounting bolt broke during removal.")]
    [InlineData("The fastener was not seized.")]
    [InlineData("Final alignment passed without correction.")]
    [InlineData("No additional dismantling work was required.")]
    public void Negated_or_normal_events_produce_no_complication(string text)
    {
        Assert.Empty(RepairComplicationClassifier.Classify(text));
    }
}
