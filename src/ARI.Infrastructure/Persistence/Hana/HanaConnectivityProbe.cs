using Sap.Data.Hana;

namespace ARI.Infrastructure.Persistence.Hana;

public sealed class HanaConnectivityProbe(
    IHanaConnectionFactory connectionFactory,
    HanaOptions options) : IHanaConnectivityProbe
{
    private const string CurrentUserSql = "SELECT CURRENT_USER FROM DUMMY";

    public async Task<HanaConnectivityResult> CheckAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await connectionFactory
                .OpenConnectionAsync(cancellationToken)
                .ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = CurrentUserSql;
            command.CommandTimeout = options.ConnectionTimeoutSeconds;

            var scalar = await command
                .ExecuteScalarAsync(cancellationToken)
                .ConfigureAwait(false);
            var currentUser = Convert.ToString(
                scalar,
                System.Globalization.CultureInfo.InvariantCulture);

            if (string.IsNullOrWhiteSpace(currentUser))
            {
                throw new InvalidOperationException(
                    "SAP HANA did not return an authenticated user.");
            }

            if (!string.Equals(currentUser, options.User.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The authenticated SAP HANA user does not match Hana:User.");
            }

            return new HanaConnectivityResult(currentUser);
        }
        catch (HanaException exception)
        {
            throw new HanaConnectivityException(
                exception.NativeError,
                exception.SqlState,
                HanaDiagnosticSanitizer.Sanitize(exception.Message, options),
                exception);
        }
    }
}
