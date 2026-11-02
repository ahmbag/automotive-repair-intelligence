namespace ARI.Infrastructure.Persistence.Hana;

public sealed class HanaPersistenceException(
    int nativeError,
    string? sqlState,
    string safeDiagnostic,
    Exception innerException)
    : Exception("SAP HANA persistence operation failed.", innerException)
{
    public int NativeError { get; } = nativeError;

    public string SqlState { get; } = sqlState ?? string.Empty;

    public string SafeDiagnostic { get; } = safeDiagnostic;
}
