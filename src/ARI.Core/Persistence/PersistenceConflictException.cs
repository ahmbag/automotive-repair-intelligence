namespace ARI.Core.Persistence;

public sealed class PersistenceConflictException(string message, Exception innerException)
    : Exception(message, innerException);
