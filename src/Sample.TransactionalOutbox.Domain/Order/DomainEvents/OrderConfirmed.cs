using Sample.TransactionalOutbox.Domain.Primitives;

namespace Sample.TransactionalOutbox.Domain.Order.DomainEvents;

public sealed record class OrderConfirmed(Guid productId) : IDomainEvent { }
