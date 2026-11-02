using ARI.Infrastructure.Persistence.Hana;
using Sap.Data.Hana;

namespace ARI.Api.Tests;

public sealed class HanaConnectionFactoryTests
{
    [Fact]
    public void Connection_string_always_enables_tls_and_certificate_validation()
    {
        var options = CreateValidOptions();

        var connectionString = HanaConnectionFactory.CreateConnectionString(options);
        var parsed = new HanaConnectionStringBuilder { ConnectionString = connectionString };

        Assert.True(parsed.Encrypt);
        Assert.True(parsed.SSLValidateCertificate);
        Assert.False(parsed.PersistSecurityInfo);
        Assert.Equal("hana.example.test:443", parsed.Server);
        Assert.Equal("ARI_APP", parsed.UserID);
    }

    [Fact]
    public void Missing_password_is_rejected_without_opening_a_connection()
    {
        var options = CreateValidOptions(password: string.Empty);

        var exception = Assert.Throws<HanaConfigurationException>(
            () => HanaConnectionFactory.CreateConnectionString(options));

        Assert.Equal("Hana:Password is required.", exception.Message);
    }

    [Fact]
    public void Diagnostic_sanitizer_removes_credentials_and_endpoint()
    {
        var options = CreateValidOptions();
        var diagnostic =
            $"Connection failed: password={options.Password}; user={options.User}; host={options.Host}";

        var sanitized = HanaDiagnosticSanitizer.Sanitize(diagnostic, options);

        Assert.DoesNotContain(options.Password, sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(options.User, sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(options.Host, sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<password>", sanitized);
        Assert.Contains("<user>", sanitized);
        Assert.Contains("<host>", sanitized);
    }

    private static HanaOptions CreateValidOptions(string password = "not-a-real-secret") =>
        new()
        {
            Host = "hana.example.test",
            Port = 443,
            User = "ARI_APP",
            Password = password,
            ConnectionTimeoutSeconds = 10
        };
}
