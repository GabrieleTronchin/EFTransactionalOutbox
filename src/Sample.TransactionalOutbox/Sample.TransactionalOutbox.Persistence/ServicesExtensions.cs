using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Sample.TransactionalOutbox.Domain.Order;
using Sample.TransactionalOutbox.Domain.Product;
using Sample.TransactionalOutbox.Persistence.Interceptors;
using Sample.TransactionalOutbox.Persistence.Repository;

namespace Sample.TransactionalOutbox.Persistence
{
    public static class ServicesExtensions
    {
        public static IServiceCollection AddPersistence(this IServiceCollection services)
        {
            services.AddTransient<IProductRepository, ProductRepository>();
            services.AddTransient<IOrderRepository, OrderRepository>();
            services.AddSingleton<OrderDomainEventInterceptor>();

            var tcInterceptor = services.BuildServiceProvider().GetRequiredService<OrderDomainEventInterceptor>();

            services.AddDbContext<ShopDbContext>(options =>
            {
                options.UseInMemoryDatabase("ShopDb")
                    .AddInterceptors(tcInterceptor)
                    .EnableSensitiveDataLogging()
                    .ConfigureWarnings(b => b.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            });

            return services;

        }
    }
}
