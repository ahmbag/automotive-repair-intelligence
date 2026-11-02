namespace ARI.Infrastructure.Persistence.Hana;

public sealed class HanaConfigurationException(string message) : InvalidOperationException(message);
