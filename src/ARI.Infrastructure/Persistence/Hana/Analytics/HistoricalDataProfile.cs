namespace ARI.Infrastructure.Persistence.Hana.Analytics;

public sealed record LaborStatistics(
    double MeanReferenceMinutes,
    double MedianReferenceMinutes,
    double MeanActualMinutes,
    double MedianActualMinutes,
    double P90ActualMinutes,
    double MeanAbsoluteDeviationMinutes,
    double MeanRelativeDeviationPercent);

public sealed record CorrosionComparison(
    int KnownCorrosionCases,
    int OtherCases,
    double KnownCorrosionMeanDeviationMinutes,
    double OtherMeanDeviationMinutes,
    double KnownCorrosionComplicationRatePercent,
    double OtherComplicationRatePercent);

public sealed record HistoricalDataProfile(
    string DatasetId,
    string ImportStatus,
    string DataClassification,
    int ExpectedRecords,
    int ImportedRecords,
    int RepairOrders,
    int CompleteCases,
    int OperationCount,
    int ExecutionCount,
    int ComplicationCount,
    int ConditionCount,
    int OrphanCount,
    int InvalidLaborCount,
    int InvalidVehicleCount,
    int InvalidTimelineCount,
    int MissingReportCount,
    LaborStatistics Labor,
    CorrosionComparison Corrosion,
    IReadOnlyDictionary<string, int> ComplicationsByCode,
    IReadOnlyList<string> ValidationErrors)
{
    public bool IsValid => ValidationErrors.Count == 0;
}
