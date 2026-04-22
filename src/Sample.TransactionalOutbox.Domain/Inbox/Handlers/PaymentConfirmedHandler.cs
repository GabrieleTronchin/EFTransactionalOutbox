using MediatR;
using Microsoft.Extensions.Logging;
using Sample.TransactionalOutbox.Domain.Order;

namespace Sample.TransactionalOutbox.Domain.Inbox.Handlers;

internal sealed class PaymentConfirmedHandler : INotificationHandler<PaymentConfirmedInboxMessage>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<PaymentConfirmedHandler> _logger;

    public PaymentConfirmedHandler(
        IOrderRepository orderRepository,
        ILogger<PaymentConfirmedHandler> logger
    )
    {
        _orderRepository = orderRepository;
        _logger = logger;
    }

    public async Task Handle(PaymentConfirmedInboxMessage notification, CancellationToken cancellationToken)
    {
        if (notification.OrderId == Guid.Empty)
            throw new InvalidOperationException("OrderId cannot be empty.");

        _logger.LogInformation(
            "Processing PaymentConfirmed for OrderId = {OrderId}",
            notification.OrderId
        );

        var order = await _orderRepository.GetAsync(notification.OrderId, cancellationToken);

        order.ConfirmPayment();
    }
}
