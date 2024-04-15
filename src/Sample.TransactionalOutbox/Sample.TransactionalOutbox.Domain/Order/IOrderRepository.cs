using Sample.TransactionalOutbox.Domain.Primitives;

namespace Sample.TransactionalOutbox.Domain.Order;

public interface IOrderRepository : IRepository<OrderEntity>
{
    Task<OrderEntity> GetAsync(Guid id, CancellationToken cancel);

    Task<IEnumerable<OrderEntity>> GetAsync(CancellationToken cancel);
}