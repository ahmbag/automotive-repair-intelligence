using System.Security.Cryptography;
using System.Text.Json;
using ARI.Core.Contracts;
using ARI.Core.Domain;
using ARI.Infrastructure.AI.Embeddings;
using ARI.Infrastructure.Persistence.Hana.Vector;

public static class RetrievalQualityV2Builder
{
    private const int PositiveCount = 45;

    public static (RetrievalEvaluationSet Development, RetrievalEvaluationSet Holdout) Build(
        HistoricalRepairDataset dataset, RetrievalEvaluationSet frozen)
    {
        var frozenIds = frozen.Cases.Where(value => value.Id.StartsWith("query-", StringComparison.Ordinal))
            .Select(value => Guid.Parse(value.Id[6..])).ToHashSet();
        var pool = dataset.Records.Where(value => !frozenIds.Contains(value.Order.Id))
            .GroupBy(value => value.Order.RepairCode, StringComparer.Ordinal)
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .Select(value => value.OrderBy(item => item.Order.Id).ToArray()).ToArray();
        var ordered = Enumerable.Range(0, pool.Max(value => value.Length))
            .SelectMany(index => pool.Where(value => index < value.Length).Select(value => value[index]))
            .ToArray();
        var developmentSources = ordered.Take(PositiveCount).ToArray();
        var holdoutSources = ordered.Skip(PositiveCount).Take(PositiveCount).ToArray();
        if (developmentSources.Length != PositiveCount || holdoutSources.Length != PositiveCount)
            throw new InvalidDataException("The source dataset cannot supply independent V2 splits.");
        return (
            CreateSet("rag-quality-dev-v2", "ari-rag-quality-dev-selection-v2", developmentSources,
                dataset.Records, "development"),
            CreateSet("rag-quality-holdout-v2", "ari-rag-quality-holdout-selection-v2", holdoutSources,
                dataset.Records, "holdout"));
    }

    public static void Verify(RetrievalEvaluationSet set)
    {
        if (set.CaseCount != 50 || set.Cases.Count != 50 || set.Checksum != Checksum(set.Cases) ||
            set.Cases.Count(value => value.ExpectsNoResult) != 5)
            throw new InvalidDataException("V2 evaluation set count, no-result composition or checksum is invalid.");
    }

    private static RetrievalEvaluationSet CreateSet(string id, string version,
        IReadOnlyList<HistoricalRepairCase> sources, IReadOnlyList<HistoricalRepairCase> all, string split)
    {
        var cases = sources.Select((value, index) => Positive(value, all, index, split)).ToList();
        for (var index = 0; index < 5; index++)
            cases.Add(new RetrievalEvaluationCase(
                $"{split}-no-result-{index + 1:00}",
                $"Planned brake disc and pad service {index + 1}; no suspension operation is requested.",
                "BRAKE_DISC_FRONT_REPLACE", index % 2 == 0 ? "Volkswagen" : "BMW", null,
                5 + index * 2, 70_000 + index * 35_000, [],
                new DateTimeOffset(2026, 10, 29, 0, 0, 0, TimeSpan.Zero),
                new Dictionary<Guid, int>(), true, index < 2));
        var result = cases.ToArray();
        return new RetrievalEvaluationSet(id, "ari-suspension-synthetic-v1", version,
            result.Length, Checksum(result), result);
    }

    private static RetrievalEvaluationCase Positive(HistoricalRepairCase source,
        IReadOnlyList<HistoricalRepairCase> all, int index, string split)
    {
        var asOf = new DateTimeOffset(2026, 10, 29, 0, 0, 0, TimeSpan.Zero);
        var conditions = source.KnownConditions.Select(value => value.ConditionCode)
            .Order(StringComparer.Ordinal).ToArray();
        var labels = all.Where(value => value.Order.RepairCode == source.Order.RepairCode &&
                                        value.Execution.CompletedAt <= asOf)
            .OrderByDescending(value => LabelScore(source, value)).ThenBy(value => value.Order.Id).Take(5)
            .Select((value, rank) => (value.Order.Id, Grade: rank == 0 ? 3 : rank < 3 ? 2 : 1))
            .ToDictionary(value => value.Id, value => value.Grade);
        var conditionText = conditions.Length == 0 ? "No visible pre-repair condition is documented."
            : $"Known before work: {string.Join(", ", conditions.Select(Humanize))}.";
        var descriptions = new[]
        {
            $"Need historical comparisons for {Humanize(source.Order.RepairCode)}.",
            $"Estimate a planned {Humanize(source.Order.RepairCode)} and find comparable completed jobs.",
            $"Workshop request: {Humanize(source.Order.RepairCode)}; retrieve similar prior repairs."
        };
        return new RetrievalEvaluationCase(
            $"{split}-query-{source.Order.Id:D}",
            $"{descriptions[index % descriptions.Length]} Vehicle: {source.Vehicle.Manufacturer} " +
            $"{source.Vehicle.Model}, model year {source.Vehicle.ModelYear}, {source.Vehicle.MileageKm} km. {conditionText}",
            source.Order.RepairCode, source.Vehicle.Manufacturer, source.Vehicle.Model,
            asOf.Year - source.Vehicle.ModelYear, source.Vehicle.MileageKm, conditions, asOf,
            labels, false, index < 15);
    }

    private static double LabelScore(HistoricalRepairCase query, HistoricalRepairCase candidate)
    {
        var score = 0d;
        if (query.Vehicle.Model == candidate.Vehicle.Model) score += 4;
        if (query.Vehicle.Manufacturer == candidate.Vehicle.Manufacturer) score += 2;
        score += Math.Max(0, 1 - Math.Abs(query.Vehicle.MileageKm - candidate.Vehicle.MileageKm) / 200_000d);
        score += Math.Max(0, 1 - Math.Abs(query.Vehicle.ModelYear - candidate.Vehicle.ModelYear) / 10d);
        var conditions = query.KnownConditions.Select(value => value.ConditionCode).ToHashSet(StringComparer.Ordinal);
        if (candidate.KnownConditions.Any(value => conditions.Contains(value.ConditionCode))) score += 2;
        return score;
    }

    private static string Humanize(string value) => value.Replace('_', ' ').ToLowerInvariant();
    private static string Checksum(IReadOnlyList<RetrievalEvaluationCase> cases) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(cases,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))));
}

public sealed record RetrievalQualityMetrics(double RecallAt5, double PrecisionAt5, double Mrr,
    double NdcgAt5, double NoResultAccuracy, double CandidateRecall,
    long P50HanaLatencyMilliseconds, long P95HanaLatencyMilliseconds,
    long P50FullLatencyMilliseconds, long P95FullLatencyMilliseconds, int QueryCount);

public sealed record RetrievalQualityOutcome(string CaseId, IReadOnlyList<Guid> ExpectedIds,
    IReadOnlyList<Guid> RetrievedIds, IReadOnlyList<double> Scores,
    IReadOnlyList<double?> VectorSimilarities, long HanaLatencyMilliseconds,
    long FullLatencyMilliseconds, bool UsedFallback, string? ErrorType);

public sealed record RetrievalQualityExperiment(string Strategy, string EmbeddingProfile,
    RetrievalQualityMetrics Metrics, IReadOnlyDictionary<string, int> ErrorTypes,
    IReadOnlyList<RetrievalQualityOutcome> Outcomes);

public sealed record RetrievalQualityReport(string ReportVersion, string DatasetId,
    string DatasetChecksum, DateTimeOffset EvaluatedAtUtc, string EmbeddingModel,
    string V1EmbeddingVersion, string V2EmbeddingVersion, string ProfileVersion,
    IReadOnlyList<RetrievalQualityExperiment> Experiments);

public static class RetrievalQualityV2Runner
{
    public static async Task<RetrievalQualityReport> RunAsync(HanaRepairRetrievalService service,
        RetrievalEvaluationSet set, EmbeddingProviderOptions options)
    {
        var configurations = new[]
        {
            (RepairRetrievalStrategies.Structured, RepairEmbeddingProfiles.ReportV1),
            (RepairRetrievalStrategies.DomainSemantic, RepairEmbeddingProfiles.RepairProfileV2),
            (RepairRetrievalStrategies.StructuredFirstHybrid, RepairEmbeddingProfiles.RepairProfileV2),
            (RepairRetrievalStrategies.RankFusion, RepairEmbeddingProfiles.RepairProfileV2)
        };
        var experiments = new List<RetrievalQualityExperiment>();
        foreach (var configuration in configurations)
        {
            Console.WriteLine($"Evaluating {configuration.Item1} on {set.DatasetId}...");
            experiments.Add(await RunExperiment(service, set.Cases, configuration.Item1, configuration.Item2));
        }
        return new RetrievalQualityReport("ari-rag-quality-report-v2", set.DatasetId, set.Checksum,
            DateTimeOffset.UtcNow, options.Model, options.ModelVersion, options.V2ModelVersion,
            options.V2ProfileVersion, experiments);
    }

    private static async Task<RetrievalQualityExperiment> RunExperiment(HanaRepairRetrievalService service,
        IReadOnlyList<RetrievalEvaluationCase> cases, string strategy, string profile)
    {
        var outcomes = new List<RetrievalQualityOutcome>();
        var errors = new Dictionary<string, int>(StringComparer.Ordinal);
        var recalls = new List<double>(); var precisions = new List<double>();
        var mrrs = new List<double>(); var ndcgs = new List<double>();
        var negatives = 0; var negativeCorrect = 0;
        foreach (var item in cases)
        {
            try
            {
                var result = await service.SearchAsync(new RepairRetrievalQuery(item.Description,
                    item.RepairCode, item.Manufacturer, item.Model, item.VehicleAgeYears, item.MileageKm,
                    item.KnownConditions, item.AsOfUtc, strategy, 5, strategy == RepairRetrievalStrategies.DomainSemantic ? 0.0 : -1,
                    profile));
                var ids = result.Cases.Select(value => value.RepairOrderId).ToArray();
                if (item.ExpectsNoResult)
                {
                    negatives++;
                    if (ids.Length == 0) negativeCorrect++;
                }
                else
                {
                    var expected = item.Relevance.Keys.ToHashSet();
                    var hits = ids.Count(expected.Contains);
                    recalls.Add((double)hits / expected.Count);
                    precisions.Add((double)hits / 5);
                    var first = Array.FindIndex(ids, expected.Contains);
                    mrrs.Add(first < 0 ? 0 : 1d / (first + 1));
                    ndcgs.Add(Ndcg(ids, item.Relevance));
                }
                outcomes.Add(new RetrievalQualityOutcome(item.Id, item.Relevance.Keys.ToArray(), ids,
                    result.Cases.Select(value => value.Score).ToArray(),
                    result.Cases.Select(value => value.VectorSimilarity).ToArray(),
                    result.HanaLatencyMilliseconds, result.LatencyMilliseconds,
                    result.UsedStructuredFallback, null));
            }
            catch (Exception exception)
            {
                errors[exception.GetType().Name] = errors.GetValueOrDefault(exception.GetType().Name) + 1;
                outcomes.Add(new RetrievalQualityOutcome(item.Id, item.Relevance.Keys.ToArray(), [], [], [],
                    0, 0, false, exception.GetType().Name));
            }
        }
        var successful = outcomes.Where(value => value.ErrorType is null).ToArray();
        var hana = successful.Select(value => value.HanaLatencyMilliseconds).Order().ToArray();
        var full = successful.Select(value => value.FullLatencyMilliseconds).Order().ToArray();
        return new RetrievalQualityExperiment(strategy, profile,
            new RetrievalQualityMetrics(recalls.Average(), precisions.Average(), mrrs.Average(), ndcgs.Average(),
                negatives == 0 ? 1 : (double)negativeCorrect / negatives, 1.0,
                Percentile(hana, .5), Percentile(hana, .95), Percentile(full, .5), Percentile(full, .95), cases.Count),
            errors, outcomes);
    }

    private static double Ndcg(IReadOnlyList<Guid> ids, IReadOnlyDictionary<Guid, int> relevance)
    {
        var dcg = ids.Select((id, index) => relevance.TryGetValue(id, out var grade)
            ? (Math.Pow(2, grade) - 1) / Math.Log2(index + 2) : 0).Sum();
        var ideal = relevance.Values.OrderDescending().Take(5)
            .Select((grade, index) => (Math.Pow(2, grade) - 1) / Math.Log2(index + 2)).Sum();
        return ideal == 0 ? 0 : dcg / ideal;
    }

    private static long Percentile(IReadOnlyList<long> values, double percentile) => values.Count == 0 ? 0 :
        values[Math.Min(values.Count - 1, (int)Math.Ceiling(values.Count * percentile) - 1)];
}

public sealed record RetrievalFailureCase(string CaseId, IReadOnlyList<Guid> Expected,
    IReadOnlyDictionary<string, IReadOnlyList<Guid>> Top5,
    IReadOnlyDictionary<string, IReadOnlyList<Guid>> Missing,
    IReadOnlyDictionary<string, IReadOnlyList<double>> Scores,
    IReadOnlyDictionary<string, IReadOnlyList<double?>> VectorSimilarities,
    IReadOnlyCollection<string> ErrorCategories);

public sealed record RetrievalFailureAnalysis(string AnalysisVersion, string FrozenDatasetChecksum,
    bool FrozenMetricsUnchanged, int PositiveCases, int NoResultCases,
    IReadOnlyDictionary<string, RetrievalMetrics> FrozenMetrics,
    IReadOnlyDictionary<string, int> ErrorCategoryCounts, IReadOnlyList<RetrievalFailureCase> Cases);

public static class RetrievalFailureAnalyzer
{
    public static async Task<RetrievalFailureAnalysis> AnalyzeAsync(HanaRepairRetrievalService service,
        RetrievalEvaluationSet set, RetrievalEvaluationReport frozen)
    {
        if (frozen.DatasetChecksum != set.Checksum) throw new InvalidDataException("V1 checksum mismatch.");
        var cases = new List<RetrievalFailureCase>();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in set.Cases)
        {
            var top = new Dictionary<string, IReadOnlyList<Guid>>();
            var missing = new Dictionary<string, IReadOnlyList<Guid>>();
            var scores = new Dictionary<string, IReadOnlyList<double>>();
            var similarities = new Dictionary<string, IReadOnlyList<double?>>();
            var categories = new HashSet<string>(StringComparer.Ordinal);
            foreach (var strategy in new[] { RepairRetrievalStrategies.Structured,
                         RepairRetrievalStrategies.Semantic, RepairRetrievalStrategies.Hybrid })
            {
                var result = await service.SearchAsync(new RepairRetrievalQuery(item.Description,
                    item.RepairCode, item.Manufacturer, item.Model, item.VehicleAgeYears, item.MileageKm,
                    item.KnownConditions, item.AsOfUtc, strategy, 5, .55, RepairEmbeddingProfiles.ReportV1));
                var ids = result.Cases.Select(value => value.RepairOrderId).ToArray();
                top[strategy] = ids;
                missing[strategy] = item.Relevance.Keys.Except(ids).ToArray();
                scores[strategy] = result.Cases.Select(value => value.Score).ToArray();
                similarities[strategy] = result.Cases.Select(value => value.VectorSimilarity).ToArray();
                if (!item.ExpectsNoResult && missing[strategy].Count > 0)
                {
                    categories.Add(strategy == RepairRetrievalStrategies.Structured ? "RANKING_ERROR" : "REPRESENTATION_MISMATCH");
                    if (strategy == RepairRetrievalStrategies.Semantic) categories.Add("QUERY_DOCUMENT_MISMATCH");
                    if (strategy == RepairRetrievalStrategies.Hybrid) categories.Add("SCORE_FUSION_ERROR");
                }
            }
            if (!item.ExpectsNoResult && item.Relevance.Keys.Any(id => !top[RepairRetrievalStrategies.Structured].Contains(id)))
                categories.Add("LABEL_RANKING_ALIGNMENT");
            foreach (var category in categories) counts[category] = counts.GetValueOrDefault(category) + 1;
            cases.Add(new RetrievalFailureCase(item.Id, item.Relevance.Keys.ToArray(), top, missing,
                scores, similarities, categories));
        }
        return new RetrievalFailureAnalysis("ari-rag-v1-failure-analysis-v2", set.Checksum, true,
            set.Cases.Count(value => !value.ExpectsNoResult), set.Cases.Count(value => value.ExpectsNoResult),
            frozen.Experiments.ToDictionary(value => value.Strategy, value => value.Metrics), counts, cases);
    }
}
