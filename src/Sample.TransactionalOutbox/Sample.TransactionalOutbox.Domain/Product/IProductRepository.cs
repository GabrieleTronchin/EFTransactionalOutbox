using Sample.TransactionalOutbox.Domain.Primitives;

namespace Sample.TransactionalOutbox.Domain.Product;

public interface IProductRepository : IRepository<ProductEntity>
{
    Task<ProductEntity> GetAsync(Guid id, CancellationToken cancel);
}