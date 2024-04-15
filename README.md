# Transactional Outbox Pattern With EF Core

In this project, I've implemented the Transactional Outbox Pattern using EF Core. 
For testing purposes, we're utilizing an in-memory database.

We have two main entities:
1. Product
2. Order

These entities are automatically initialized within the database.

Within the API, there are three crucial endpoints:
1. **Products**: This endpoint returns a list of products along with their respective quantities.
2. **Orders**: Here, you can retrieve a list of created orders.
3. **OrderConfirmations**: This is a POST endpoint designed to confirm a created order.

To effectively test the project, follow these steps:
1. **Retrieve a Product**: Note that the default order quantity is set to 10.
2. **Fetch Orders**: This step allows you to obtain an order ID.
3. **Confirm an Order**: Utilize the OrderConfirmations endpoint with a valid order ID.
4. **Check Product Quantity**: Finally, re-access the product to observe the decremented order quantity.

# Transactional Outbox Pattern

//TODO

# Technical Description

Lets look a bit bejnd the hood.

The key concept are:



## Outbox Processor Job

It's a timer job that every few second consult the DomainEvents table and dispatch the event.
The timer job is implmented using Quartz .net here the link for more information: [quazlink]

In this example we use mediatR nuget to manage events, but in case you can use your favorite service bus client.

here the code of the outboxmessage pattern:

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