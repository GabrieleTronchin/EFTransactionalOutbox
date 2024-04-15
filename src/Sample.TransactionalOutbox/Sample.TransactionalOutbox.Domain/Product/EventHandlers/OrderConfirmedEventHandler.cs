using MediatR;
using Sample.TransactionalOutbox.Domain.Order.DomainEvents;

namespace Sample.TransactionalOutbox.Domain.Product.EventHandlers;

internal sealed class OrderConfirmedEventHandler : INotificationHandler<OrderConfirmed>
{
    private readonly IProductRepository _productRepository;

    public OrderConfirmedEventHandler(IProductRepository repository)
    {
        _productRepository = repository;
    }

    public async Task Handle(OrderConfirmed orderConfirmed, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetAsync(orderConfirmed.productId, cancellationToken);

        product.HasBeenPurchased();
        await _productRepository.SaveChangesAsync();
    }
}
