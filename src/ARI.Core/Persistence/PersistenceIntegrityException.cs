namespace ARI.Core.Persistence;

public sealed class PersistenceIntegrityException(string message, Exception innerException)
    : Exception(message, innerException);
