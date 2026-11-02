using System.Data.Common;
using Sap.Data.Hana;

namespace ARI.Infrastructure.Persistence.Hana;

public sealed class HanaConnectionFactory(HanaOptions options) : IHanaConnectionFactory
{
    public async Task<DbConnection> OpenConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        var connection = new HanaConnection(CreateConnectionString(options));

        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    internal static string CreateConnectionString(HanaOptions configuredOptions)
    {
        Validate(configuredOptions);

        var builder = new HanaConnectionStringBuilder
        {
            Server = $"{configuredOptions.Host.Trim()}:{configuredOptions.Port}",
            UserID = configuredOptions.User.Trim(),
            Password = configuredOptions.Password,
            Encrypt = true,
            SSLValidateCertificate = true,
            PersistSecurityInfo = false,
            ConnectionTimeout = configuredOptions.ConnectionTimeoutSeconds
        };

        return builder.ConnectionString;
    }

    private static void Validate(HanaOptions configuredOptions)
    {
        ArgumentNullException.ThrowIfNull(configuredOptions);

        if (string.IsNullOrWhiteSpace(configuredOptions.Host)
            || Uri.CheckHostName(configuredOptions.Host.Trim()) is UriHostNameType.Unknown)
        {
            throw new HanaConfigurationException("Hana:Host must be a valid host name or IP address.");
        }

        if (configuredOptions.Port is < 1 or > 65_535)
        {
            throw new HanaConfigurationException("Hana:Port must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(configuredOptions.User))
        {
            throw new HanaConfigurationException("Hana:User is required.");
        }

        if (string.IsNullOrWhiteSpace(configuredOptions.Password))
        {
            throw new HanaConfigurationException("Hana:Password is required.");
        }

        if (configuredOptions.ConnectionTimeoutSeconds is < 1 or > 120)
        {
            throw new HanaConfigurationException(
                "Hana:ConnectionTimeoutSeconds must be between 1 and 120.");
        }
    }
}
