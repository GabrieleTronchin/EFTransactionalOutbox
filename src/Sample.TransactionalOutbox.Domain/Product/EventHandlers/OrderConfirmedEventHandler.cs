using MediatR;
using Microsoft.Extensions.Logging;
using Sample.TransactionalOutbox.Domain.Order.DomainEvents;

namespace Sample.TransactionalOutbox.Domain.Product.EventHandlers;

internal sealed class OrderConfirmedEventHandler : INotificationHandler<OrderConfirmed>
{
    private readonly IProductRepository _productRepository;
    private readonly ILogger<OrderConfirmedEventHandler> _logger;

    public OrderConfirmedEventHandler(
        IProductRepository repository,
        ILogger<OrderConfirmedEventHandler> logger
    )
    {
        _productRepository = repository;
        _logger = logger;
    }

    public async Task Handle(OrderConfirmed orderConfirmed, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "New order confirmation received OrderId = {OrderId}, ProductId = {ProductId}",
            orderConfirmed.OrderId,
            orderConfirmed.ProductId
        );

        var product = await _productRepository.GetAsync(
            orderConfirmed.ProductId,
            cancellationToken
        );

        product.HasBeenConfirmed();

        await _productRepository.SaveChangesAsync();
    }
}
