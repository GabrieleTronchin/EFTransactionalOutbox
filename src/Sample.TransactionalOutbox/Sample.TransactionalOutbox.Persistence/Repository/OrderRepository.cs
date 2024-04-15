using Microsoft.EntityFrameworkCore;
using Sample.TransactionalOutbox.Domain.Order;

namespace Sample.TransactionalOutbox.Persistence.Repository;

internal class OrderRepository : IOrderRepository
{
    private readonly ShopDbContext _context;

    public OrderRepository(ShopDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(OrderEntity entity)
    {
        await _context.AddAsync(entity);
    }

    public async Task<OrderEntity> GetAsync(Guid id, CancellationToken cancel)
    {
        return await _context.Orders.SingleOrDefaultAsync(x => x.Id == id, cancel) ??
         throw new InvalidOperationException($"System could not find any {nameof(OrderEntity.Id)} with value {id}");
    }

    public async Task<IEnumerable<OrderEntity>> GetAsync(CancellationToken cancel)
    {
        return await _context.Orders.ToListAsync(cancel);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
