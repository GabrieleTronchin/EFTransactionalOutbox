using Sample.TransactionalOutbox.Domain.Order.DomainEvents;
using Sample.TransactionalOutbox.Domain.Primitives;

namespace Sample.TransactionalOutbox.Domain.Order;

public class OrderEntity : DomainEventManager
{
    private OrderEntity() { }

    public static OrderEntity Create(Guid productId, string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException($"Invalid {nameof(description)}");

        var order = new OrderEntity
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Description = description,
            Confirmed = false,
        };

        return order;
    }

    public void ConfirmPayment()
    {
        if (Confirmed)
            throw new InvalidOperationException("It's already confirmed.");

        RaiseEvent(new OrderConfirmed(ProductId));

        Confirmed = true;
    }

    public Guid Id { get; private set; }

    public Guid ProductId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public bool Confirmed { get; private set; }
}
