using System.Data.Common;

namespace ARI.Infrastructure.Persistence.Hana;

public interface IHanaConnectionFactory
{
    Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}
