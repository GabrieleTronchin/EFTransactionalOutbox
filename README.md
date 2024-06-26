# Transactional Outbox Pattern With EF Core

In this project, I've implemented the Transactional Outbox Pattern using EF Core. 
For testing purposes, we're utilizing an in-memory database.

We have two main entities:
1. Product
2. Order

These entities are automatically initialized within the database.

Within the API, there are three crucial endpoints:

![Swagger](assets/SwaggerHome.png)

1. **Products**: This endpoint returns a list of products along with their respective quantities.
2. **Orders**: Here, you can retrieve a list of created orders.
3. **PurchaseOrder**: This is a POST endpoint designed to confirm a created order.

To effectively test the project, follow these steps:
1. **Retrieve a Product**: Note that the default order quantity is set to 10.
    ![GetProduct](assets/GetProduct01.png)
2. **Fetch Orders**: This step allows you to obtain an order ID.
   ![GetOrder](assets/GetOrder01.png)
3. **Purchase Order**: Utilize the PurchaseOrder endpoint with a valid order ID.
   ![Purchase Order](assets/PurchaseOrder.png)
4. **Check Product Quantity**: Finally, re-access the product to observe the decremented order quantity.
    ![Swagger](assets/GetProduct02.png)

# Transactional Outbox Pattern

In a microservices architecture, various services are typically responsible for distinct aspects of business logic. 
However, when an operation necessitates the involvement of multiple services, maintaining data consistency becomes a significant challenge. 

For instance, consider an Order Management System where the process of placing an order entails not only updating inventory but also several other operations. 
Failure at any of these stages can result in inconsistencies across the services.

In this article, we will delve into the Transactional Outbox pattern within the context of Order Management and provide a simplified example. 

To simplify the explanation, I have utilized SQL Server to persist our events, Quartz for event consumption, and Mediator as a message broker. 
It's worth noting that different databases can be employed to persist events, and a service bus can be utilized to dispatch them, offering flexibility in implementation.

More info about  Transactional Outbox Pattern [here](https://microservices.io/patterns/data/transactional-outbox.html)

# Technical Description

Let's take a peek under the hood of our system.

In the persistence layer, we've set up a table dedicated to storing domain message events.

I've introduced an abstract class named "DomainEventManager," responsible for managing a list of events.
This class can be inherited from a domain class and use it to enqueued events. 

When the "SaveChanges" method is invoked, an Entity Framework interceptor takes charge of persisting all domain events into our designated table.

Additionally, a timer job is activated every few seconds. Its duty is to peruse the event table and dispatch the events stored within.

## Domain Events Manager

Our manager is tasked with handling events.
Let's delve into the code:

```C#
public abstract class DomainEventManager
{
    private readonly IList<IDomainEvent> _events;

    protected DomainEventManager()
    {
        _events = new List<IDomainEvent>();
    }

    public void RaiseEvent(IDomainEvent domainEvent)
    {
        _events.Add(domainEvent);
    }

    public IEnumerable<IDomainEvent> GetEvents()
    {
        return _events.ToList();
    }

    public void ClearEvents()
    {
        _events.Clear();
    }
}
```

We can inherit DomainEventManager class into other domain classes. 
This allows us to seamlessly integrate event handling within our domain logic.

```C#
public class OrderEntity : DomainEventManager
{
    
    // Code omitted for brevity

    public void ConfirmPayment()
    {
        if (Confirmed) throw new InvalidOperationException("It's already confirmed.");

        RaiseEvent(new OrderConfirmed(ProductId));

        Confirmed = true;
    }

    // Code omitted for brevity

}
```


## Entity Framework Save DomainEvents

To automatically persist events, a custom SaveChange method is necessary.
For this purpose, we've implemented the "SaveChangesInterceptor" class. 
 
Here more information about EntityFramework [Interceptors](https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/interceptors)

 Here's a snippet of the interceptor code:

```C#
public sealed class OrderDomainEventInterceptor
: SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var dbContext = eventData.Context;

        if (dbContext is null)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        var domainEvents = dbContext.ChangeTracker.Entries<OrderEntity>()
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
                 Content = JsonConvert.SerializeObject(x, new JsonSerializerSettings
                 {
                     TypeNameHandling = TypeNameHandling.All
                 })
             })
             .ToList();

        dbContext.Set<OutboxMessageEntity>().AddRange(domainEvents);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
```


## Outbox Processor Job

The outbox processor job is a crucial component that ensures timely dispatch of events stored in the DomainEvents table. It operates using Quartz .NET. For further details, refer to this [link](https://www.quartz-scheduler.net/)

In our implementation, we leverage the MediatR NuGet package for event management. However, feel free to integrate your preferred service bus client.

Here's an overview of the outbox message pattern:
```C#
public class OutboxMessageProcessorJob : IJob
{
    private const int DEFAULT_TAKE_DOMAINS = 10;
    private readonly ShopDbContext _context;
    private readonly IPublisher _publisher;
    private readonly ILogger<OutboxMessageProcessorJob> _logger;

    public OutboxMessageProcessorJob(ILogger<OutboxMessageProcessorJob> logger,
                                     ShopDbContext context,
                                     IPublisher publisher)
    {
        _context = context;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var messages = await _context.DomainEvents
                 .Where(de => de.CompleteTime == null)
                 .Take(DEFAULT_TAKE_DOMAINS).ToListAsync(context.CancellationToken);


        if (!messages.Any()) return;

        _logger.LogDebug($"Some new messages event are found. Event number: {messages.Count}");

        foreach (var message in messages)
        {
            try
            {
                var domainEvent = JsonConvert.DeserializeObject<IDomainEvent>(message.Content, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto
                });

                if (domainEvent == null)
                {
                    _logger.LogError($"An error occurred during deserialization. Domain Event Id:{message.Id}");
                    continue;
                }

                await _publisher.Publish(domainEvent, context.CancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred processing domain event messages.");
                message.Exception = ex.Message;
            }
            finally
            {
                message.CompleteTime = DateTime.UtcNow;
            }
        }

        var sentMessages = messages.Where(de => de.Exception == null);

        _context.DomainEvents.RemoveRange(sentMessages);

        await _context.SaveChangesAsync(context.CancellationToken);
    }
}
```

This comprehensive setup guarantees efficient handling and processing of domain events within our application.
