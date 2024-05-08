using Microsoft.EntityFrameworkCore.Diagnostics;
using Newtonsoft.Json;
using Sample.TransactionalOutbox.Domain;
using Sample.TransactionalOutbox.Domain.Order;

namespace Sample.TransactionalOutbox.Persistence.Interceptors;

public sealed class OrderDomainEventInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        var dbContext = eventData.Context;

        if (dbContext is null)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        var domainEvents = dbContext
            .ChangeTracker.Entries<OrderEntity>()
            .Select(x => x.Entity)
            .SelectMany(x =>
            {
                var @event = x.GetEvents();

                x.ClearEvents();

                return @event;
            })
            .Select(x => new OutboxMessageEntity()
            {
                Id = Guid.NewGuid(),
                CreationTime = DateTime.UtcNow,
                Type = x.GetType().Name,
                Content = JsonConvert.SerializeObject(
                    x,
                    new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All }
                )
            })
            .ToList();

        dbContext.Set<OutboxMessageEntity>().AddRange(domainEvents);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
