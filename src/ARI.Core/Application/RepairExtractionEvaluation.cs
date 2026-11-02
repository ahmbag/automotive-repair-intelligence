using ARI.Core.Contracts;

namespace ARI.Core.Application;

public sealed record ExtractionEvaluationObservation(
    bool SchemaValid,
    bool VehicleExactMatch,
    bool RepairCodeExactMatch,
    IReadOnlySet<string> ExpectedComplications,
    IReadOnlySet<string> PredictedComplications,
    int UnsupportedClaims,
    int TemporalLeakageViolations,
    long LatencyMilliseconds,
    int InputTokens,
    int OutputTokens,
    string ProcessingStatus = RepairExtractionStatuses.Accepted);

public sealed record ComplicationClassMetrics(
    int TruePositives,
    int FalsePositives,
    int FalseNegatives,
    decimal Precision,
    decimal Recall,
    decimal F1);

public sealed record ExtractionEvaluationMetrics(
    int CaseCount,
    decimal SchemaValidityRate,
    decimal VehicleExactMatchAccuracy,
    decimal RepairCodeExactMatchAccuracy,
    decimal ComplicationPrecision,
    decimal ComplicationRecall,
    decimal ComplicationF1,
    decimal ComplicationMacroF1,
    decimal ComplicationSamplesF1,
    decimal AcceptedCoverageRate,
    IReadOnlyDictionary<string, ComplicationClassMetrics> PerClass,
    int UnsupportedClaimCount,
    int TemporalLeakageViolationCount,
    long TotalLatencyMilliseconds,
    int TotalInputTokens,
    int TotalOutputTokens);

/// <summary>Micro-averaged complication metrics; no unrelated measures are hidden in one score.</summary>
public static class RepairExtractionEvaluation
{
    public static ExtractionEvaluationMetrics Calculate(
        IReadOnlyCollection<ExtractionEvaluationObservation> observations)
    {
        if (observations.Count == 0)
        {
            throw new ArgumentException("At least one evaluation observation is required.");
        }

        var truePositives = observations.Sum(row =>
            row.ExpectedComplications.Intersect(row.PredictedComplications).Count());
        var falsePositives = observations.Sum(row =>
            row.PredictedComplications.Except(row.ExpectedComplications).Count());
        var falseNegatives = observations.Sum(row =>
            row.ExpectedComplications.Except(row.PredictedComplications).Count());
        var precision = Ratio(truePositives, truePositives + falsePositives);
        var recall = Ratio(truePositives, truePositives + falseNegatives);
        var f1 = precision + recall == 0m
            ? 0m
            : 2m * precision * recall / (precision + recall);
        var classes = observations
            .SelectMany(row => row.ExpectedComplications.Concat(row.PredictedComplications))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var perClass = classes.ToDictionary(
            code => code,
            code => CalculateClass(observations, code),
            StringComparer.Ordinal);
        var macroF1 = perClass.Count == 0 ? 1m : perClass.Values.Average(row => row.F1);
        var samplesF1 = observations.Average(row =>
        {
            var intersection = row.ExpectedComplications.Intersect(row.PredictedComplications).Count();
            var denominator = row.ExpectedComplications.Count + row.PredictedComplications.Count;
            return denominator == 0 ? 1m : decimal.Divide(2 * intersection, denominator);
        });

        return new ExtractionEvaluationMetrics(
            observations.Count,
            Ratio(observations.Count(row => row.SchemaValid), observations.Count),
            Ratio(observations.Count(row => row.VehicleExactMatch), observations.Count),
            Ratio(observations.Count(row => row.RepairCodeExactMatch), observations.Count),
            precision,
            recall,
            f1,
            macroF1,
            samplesF1,
            Ratio(
                observations.Count(row => row.ProcessingStatus == RepairExtractionStatuses.Accepted),
                observations.Count),
            perClass,
            observations.Sum(row => row.UnsupportedClaims),
            observations.Sum(row => row.TemporalLeakageViolations),
            observations.Sum(row => row.LatencyMilliseconds),
            observations.Sum(row => row.InputTokens),
            observations.Sum(row => row.OutputTokens));
    }

    private static ComplicationClassMetrics CalculateClass(
        IReadOnlyCollection<ExtractionEvaluationObservation> observations,
        string code)
    {
        var truePositives = observations.Count(row =>
            row.ExpectedComplications.Contains(code) && row.PredictedComplications.Contains(code));
        var falsePositives = observations.Count(row =>
            !row.ExpectedComplications.Contains(code) && row.PredictedComplications.Contains(code));
        var falseNegatives = observations.Count(row =>
            row.ExpectedComplications.Contains(code) && !row.PredictedComplications.Contains(code));
        var precision = Ratio(truePositives, truePositives + falsePositives);
        var recall = Ratio(truePositives, truePositives + falseNegatives);
        var f1 = precision + recall == 0m ? 0m : 2m * precision * recall / (precision + recall);
        return new ComplicationClassMetrics(
            truePositives, falsePositives, falseNegatives, precision, recall, f1);
    }

    private static decimal Ratio(int numerator, int denominator) =>
        denominator == 0 ? 1m : decimal.Divide(numerator, denominator);
}
