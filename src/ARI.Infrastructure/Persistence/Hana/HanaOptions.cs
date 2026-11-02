namespace ARI.Infrastructure.Persistence.Hana;

/// <summary>Credentials and endpoint settings supplied by environment variables or user secrets.</summary>
public sealed class HanaOptions
{
    public const string SectionName = "Hana";

    public string Host { get; init; } = string.Empty;

    public int Port { get; init; } = 443;

    public string User { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public int ConnectionTimeoutSeconds { get; init; } = 15;
}
