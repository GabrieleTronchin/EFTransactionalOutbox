using Sample.TransactionalOutbox.Domain.Order.DomainEvents;
using Sample.TransactionalOutbox.Domain.Primitives;

namespace Sample.TransactionalOutbox.Domain.Order;

public class OrderEntity : DomainEventManager
{
    private OrderEntity() { }

    public static OrderEntity Create(Guid productId, int quantity, decimal totalAmount, string customerName, string? shippingAddress = null)
    {
        if (string.IsNullOrWhiteSpace(customerName))
            throw new ArgumentException($"Invalid {nameof(customerName)}");

        if (quantity <= 0)
            throw new ArgumentException($"Invalid {nameof(quantity)}");

        if (totalAmount < 0)
            throw new ArgumentException($"Invalid {nameof(totalAmount)}");

        var order = new OrderEntity
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Quantity = quantity,
            TotalAmount = totalAmount,
            CustomerName = customerName,
            ShippingAddress = shippingAddress,
            OrderStatus = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        };

        return order;
    }

    public void ConfirmPayment()
    {
        if (OrderStatus != OrderStatus.Pending)
            throw new InvalidOperationException("Order is not in Pending status.");

        RaiseEvent(new OrderConfirmed(Id, ProductId));

        OrderStatus = OrderStatus.Confirmed;
        ConfirmedAt = DateTime.UtcNow;
    }

    public void CancelOrder()
    {
        if (OrderStatus != OrderStatus.Pending)
            throw new InvalidOperationException("Order is not in Pending status.");

        RaiseEvent(new OrderCancelled(Id, ProductId));

        OrderStatus = OrderStatus.Cancelled;
    }

    public Guid Id { get; private set; }

    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public decimal TotalAmount { get; private set; }
    public OrderStatus OrderStatus { get; private set; }
    public string CustomerName { get; private set; } = string.Empty;
    public string? ShippingAddress { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
}
