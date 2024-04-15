using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Sample.TransactionalOutbox.Domain.Order;
using Sample.TransactionalOutbox.Domain.Product;

namespace Sample.TransactionalOutbox.Persistence;

public static class SeedDb
{
    public static void Initialize(IApplicationBuilder app)
    {
        using var serviceScope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>().CreateScope();

        var context = serviceScope.ServiceProvider.GetService<ShopDbContext>()
            ?? throw new NullReferenceException($"Cannot find any service for {nameof(ShopDbContext)}");

        context.Database.EnsureCreated();

        var product = ProductEntity.Create(10);
        context.Products.Add(product);

        context.Orders.Add(OrderEntity.Create(product.Id, "TestProduct"));

        context.SaveChanges();
    }

}

