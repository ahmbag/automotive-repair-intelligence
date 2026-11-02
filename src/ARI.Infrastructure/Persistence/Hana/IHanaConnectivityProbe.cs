namespace ARI.Infrastructure.Persistence.Hana;

public interface IHanaConnectivityProbe
{
    Task<HanaConnectivityResult> CheckAsync(CancellationToken cancellationToken = default);
}

public sealed record HanaConnectivityResult(string AuthenticatedUser);
