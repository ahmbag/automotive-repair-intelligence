using System.Text.RegularExpressions;

namespace ARI.Infrastructure.Persistence.Hana;

internal static class HanaDiagnosticSanitizer
{
    public static string Sanitize(string message, HanaOptions options)
    {
        var sanitized = message;

        foreach (var (value, replacement) in new[]
                 {
                     (options.Password, "<password>"),
                     (options.User, "<user>"),
                     (options.Host, "<host>")
                 })
        {
            if (!string.IsNullOrEmpty(value))
            {
                sanitized = Regex.Replace(
                    sanitized,
                    Regex.Escape(value),
                    replacement,
                    RegexOptions.IgnoreCase,
                    TimeSpan.FromMilliseconds(100));
            }
        }

        sanitized = Regex.Replace(
            sanitized,
            @"(?i)(password|pwd)\s*=\s*[^;\s)]+",
            "$1=<password>",
            RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));

        return sanitized.Length <= 1_000 ? sanitized : sanitized[..1_000];
    }
}
