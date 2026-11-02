using ARI.Core.Persistence;
using Sap.Data.Hana;

namespace ARI.Infrastructure.Persistence.Hana;

internal static class HanaPersistenceErrors
{
    public static Exception Translate(HanaException exception, HanaOptions options)
    {
        var safeDiagnostic = HanaDiagnosticSanitizer.Sanitize(exception.Message, options);

        // HANA reports integrity failures with native codes even when SQLSTATE is HY000.
        if (exception.NativeError == 301
            || exception.SqlState?.StartsWith("23", StringComparison.Ordinal) == true)
        {
            if (exception.NativeError == 301
                || safeDiagnostic.Contains("unique", StringComparison.OrdinalIgnoreCase)
                || safeDiagnostic.Contains("primary key", StringComparison.OrdinalIgnoreCase))
            {
                return new PersistenceConflictException(
                    "The entity identifier already exists.", exception);
            }

            return new PersistenceIntegrityException(
                "The requested persistence operation violates a relational constraint.", exception);
        }

        if (exception.NativeError == 461)
        {
            return new PersistenceIntegrityException(
                "The requested persistence operation violates a relational constraint.", exception);
        }

        return new HanaPersistenceException(
            exception.NativeError,
            exception.SqlState,
            safeDiagnostic,
            exception);
    }
}
