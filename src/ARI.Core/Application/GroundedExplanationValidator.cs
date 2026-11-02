using ARI.Core.Contracts;

namespace ARI.Core.Application;

public static class GroundedExplanationValidator
{
    public static IReadOnlyCollection<string> Validate(
        GroundedRepairExplanation explanation,
        RepairRetrievalResult retrieval)
    {
        var errors = new List<string>();
        var allowed = retrieval.Cases
            .Select(item => item.SourceReference)
            .ToHashSet(StringComparer.Ordinal);
        var sources = retrieval.Cases.ToDictionary(
            item => item.SourceReference,
            item => item.SourceExcerpt,
            StringComparer.Ordinal);

        if (explanation.InsufficientEvidence && explanation.Observations.Count > 0)
        {
            errors.Add("Insufficient-evidence explanations cannot contain observations.");
        }

        if (!explanation.InsufficientEvidence && retrieval.Cases.Count == 0)
        {
            errors.Add("An explanation cannot claim evidence when retrieval returned no sources.");
        }

        foreach (var observation in explanation.Observations)
        {
            if (string.IsNullOrWhiteSpace(observation.Statement))
            {
                errors.Add("Every grounded observation requires a statement.");
            }

            if (observation.SourceReferences.Count == 0)
            {
                errors.Add("Every grounded observation requires at least one source reference.");
            }

            foreach (var reference in observation.SourceReferences)
            {
                if (!allowed.Contains(reference))
                {
                    errors.Add($"Unknown source reference: {reference}.");
                }
            }

            if (string.IsNullOrWhiteSpace(observation.EvidenceQuote) ||
                !observation.SourceReferences.Any(reference =>
                    sources.TryGetValue(reference, out var excerpt) &&
                    excerpt.Contains(observation.EvidenceQuote, StringComparison.Ordinal)))
            {
                errors.Add("Every grounded observation requires a verbatim quote from a cited source.");
            }
        }

        return errors;
    }
}
