namespace Sample.TransactionalOutbox.Contracts;

public record CreateOrderRequest(
    Guid ProductId,
    int Quantity,
    decimal TotalAmount,
    string CustomerName,
    string? ShippingAddress = null);
