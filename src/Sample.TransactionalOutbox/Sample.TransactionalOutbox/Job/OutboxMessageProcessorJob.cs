using MediatR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Quartz;
using Sample.TransactionalOutbox.Domain.Primitives;
using Sample.TransactionalOutbox.Persistence;

namespace Sample.TransactionalOutbox.Job;

[DisallowConcurrentExecution]
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
