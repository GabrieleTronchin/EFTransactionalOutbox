using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Quartz;
using Sample.TransactionalOutbox.Domain.Order;
using Sample.TransactionalOutbox.Persistence;

namespace Sample.TransactionalOutbox.Job;

[DisallowConcurrentExecution]
public class InboxMessageProcessorJob : IJob
{
    private const int DEFAULT_TAKE_MESSAGES = 10;
    private readonly ShopDbContext _context;
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<InboxMessageProcessorJob> _logger;

    public InboxMessageProcessorJob(
        ILogger<InboxMessageProcessorJob> logger,
        ShopDbContext context,
        IOrderRepository orderRepository
    )
    {
        _logger = logger;
        _context = context;
        _orderRepository = orderRepository;
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
                switch (message.MessageType)
                {
                    case "PaymentConfirmed":
                        await HandlePaymentConfirmed(message.Payload, context.CancellationToken);
                        break;
                    default:
                        _logger.LogWarning($"Unknown inbox message type: {message.MessageType}. Message Id: {message.Id}");
                        break;
                }

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

    private async Task HandlePaymentConfirmed(string payload, CancellationToken cancellationToken)
    {
        var paymentConfirmed = JsonConvert.DeserializeObject<PaymentConfirmedPayload>(payload);

        if (paymentConfirmed == null || paymentConfirmed.OrderId == Guid.Empty)
            throw new InvalidOperationException("Invalid PaymentConfirmed payload: missing or empty OrderId.");

        var order = await _orderRepository.GetAsync(paymentConfirmed.OrderId, cancellationToken);

        order.ConfirmPayment();
    }

    private sealed class PaymentConfirmedPayload
    {
        public Guid OrderId { get; set; }
    }
}
