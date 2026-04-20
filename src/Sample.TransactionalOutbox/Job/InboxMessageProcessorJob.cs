using MediatR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Quartz;
using Sample.TransactionalOutbox.Domain.Inbox;
using Sample.TransactionalOutbox.Domain.Primitives;
using Sample.TransactionalOutbox.Persistence;

namespace Sample.TransactionalOutbox.Job;

[DisallowConcurrentExecution]
public class InboxMessageProcessorJob : IJob
{
    private const int DEFAULT_TAKE_MESSAGES = 10;
    private readonly ShopDbContext _context;
    private readonly IPublisher _publisher;
    private readonly MessageTypeRegistry _registry;
    private readonly ILogger<InboxMessageProcessorJob> _logger;

    public InboxMessageProcessorJob(
        ILogger<InboxMessageProcessorJob> logger,
        ShopDbContext context,
        IPublisher publisher,
        MessageTypeRegistry registry
    )
    {
        _logger = logger;
        _context = context;
        _publisher = publisher;
        _registry = registry;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var messages = await _context
            .InboxMessages.Where(m => m.ProcessedAt == null)
            .Take(DEFAULT_TAKE_MESSAGES)
            .ToListAsync(context.CancellationToken);

        if (!messages.Any())
            return;

        _logger.LogDebug($"Found {messages.Count} unprocessed inbox message(s).");

        foreach (var message in messages)
        {
            if (message.ProcessedAt != null)
                continue;

            try
            {
                var type = _registry.Resolve(message.MessageType);

                if (type == null)
                {
                    _logger.LogWarning($"Unknown inbox message type: {message.MessageType}. Message Id: {message.Id}");
                    message.ProcessedAt = DateTime.UtcNow;
                    continue;
                }

                var inboxMessage = (IInboxMessage)JsonConvert.DeserializeObject(message.Payload, type)!;

                await _publisher.Publish(inboxMessage, context.CancellationToken);

                message.ProcessedAt = DateTime.UtcNow;
                message.Error = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred processing inbox message. Message Id: {message.Id}");
                message.Error = ex.Message;
                message.ProcessedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync(context.CancellationToken);
    }
}
