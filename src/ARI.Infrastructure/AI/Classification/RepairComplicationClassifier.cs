using System.Text.RegularExpressions;
using ARI.Core.Contracts;

namespace ARI.Infrastructure.AI.Classification;

/// <summary>
/// Deterministic second stage for V3. It recognizes only explicit, non-negated events and keeps
/// the complete matching source sentence as evidence. It does not infer risk from vehicle state.
/// </summary>
public static partial class RepairComplicationClassifier
{
    private sealed record Rule(string Code, Regex Pattern, string InformationTime);

    private static readonly Rule[] Rules =
    [
        new("BROKEN_BOLT", BrokenFastenerPattern(), RepairInformationTimes.DuringRepair),
        new("SEIZED_FASTENER", SeizedFastenerPattern(), RepairInformationTimes.DuringRepair),
        new("DAMAGED_BUSHING_SEAT", DamagedSeatPattern(), RepairInformationTimes.DuringRepair),
        new("ALIGNMENT_CORRECTION", AlignmentPattern(), RepairInformationTimes.AfterRepair)
    ];

    public static IReadOnlyCollection<ExtractedRepairFact> Classify(string document)
    {
        var matches = new Dictionary<string, ExtractedRepairFact>(StringComparer.Ordinal);
        foreach (Match sentenceMatch in SentencePattern().Matches(document))
        {
            var sentence = sentenceMatch.Value.Trim();
            if (sentence.Length == 0 || IsNegated(sentence))
            {
                continue;
            }

            foreach (var rule in Rules)
            {
                if (!matches.ContainsKey(rule.Code) && rule.Pattern.IsMatch(sentence))
                {
                    matches[rule.Code] = new ExtractedRepairFact(
                        rule.Code, rule.InformationTime, sentence);
                }
            }
        }

        return matches.Values.OrderBy(fact => fact.Code, StringComparer.Ordinal).ToArray();
    }

    private static bool IsNegated(string sentence) => NegationPattern().IsMatch(sentence);

    [GeneratedRegex(@"[^.!?]+[.!?]?", RegexOptions.CultureInvariant)]
    private static partial Regex SentencePattern();

    [GeneratedRegex(@"\b(no|not|without|never|did\s+not|was\s+not|were\s+not)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NegationPattern();

    [GeneratedRegex(@"\b((bolt|fastener|mounting\s+hardware).{0,55}(broke|broken|failed|fractured|snapped)|(?:broke|broken|failed|fractured|snapped).{0,55}(bolt|fastener)|bolt\s+extraction\s+and\s+replacement)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BrokenFastenerPattern();

    [GeneratedRegex(@"\b((bolt|fastener|mounting\s+hardware).{0,70}(seized|resisted\s+removal|corroded|careful\s+release|additional\s+extraction)|(?:seized|corroded).{0,45}(bolt|fastener|mounting\s+hardware))\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SeizedFastenerPattern();

    [GeneratedRegex(@"\b((bushing\s+(seat|mounting\s+area)|mounting\s+seat).{0,75}(damaged|worn|cleaning|corrective|preparation|needed\s+attention|required\s+attention)|(?:damaged|worn).{0,45}(bushing\s+seat|mounting\s+seat|bushing\s+mounting\s+area))\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DamagedSeatPattern();

    [GeneratedRegex(@"\b((alignment|wheel\s+geometry|geometry\s+check).{0,65}(correction|adjustment|further\s+correction|required)|(?:correction|adjustment).{0,45}(alignment|wheel\s+geometry|geometry\s+check))\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AlignmentPattern();
}
