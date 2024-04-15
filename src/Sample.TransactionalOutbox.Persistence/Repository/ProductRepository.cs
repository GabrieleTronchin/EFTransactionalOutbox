using Microsoft.EntityFrameworkCore;
using Sample.TransactionalOutbox.Domain.Product;

namespace Sample.TransactionalOutbox.Persistence.Repository;

internal class ProductRepository : IProductRepository
{
    private readonly ShopDbContext _context;

    public ProductRepository(ShopDbContext context)
    {
        _context = context;
    }


    public async Task<IEnumerable<ProductEntity>> GetAsync(CancellationToken cancel)
    {
        return await _context.Products.ToListAsync(cancel);
    }

    public async Task<ProductEntity> GetAsync(Guid id, CancellationToken cancel)
    {
        return await _context.Products
            .SingleOrDefaultAsync(x => x.Id == id, cancel) ??
             throw new InvalidOperationException($"System could not find any {nameof(ProductEntity.Id)} with value {id}");
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task AddAsync(ProductEntity entity)
    {
        await _context.AddAsync(entity);
    }

}
