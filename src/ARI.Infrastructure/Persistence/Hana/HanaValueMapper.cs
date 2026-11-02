namespace ARI.Infrastructure.Persistence.Hana;

internal static class HanaValueMapper
{
    public static DateTime ToDatabaseTimestamp(DateTimeOffset value) => value.UtcDateTime;

    public static DateTimeOffset FromDatabaseTimestamp(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public static string NormalizeRepairCode(string repairCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repairCode);
        return repairCode.Trim().ToUpperInvariant();
    }
}
