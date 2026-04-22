using Sample.TransactionalOutbox.Domain.Primitives;

namespace Sample.TransactionalOutbox.Domain.Inbox;

public sealed record PaymentConfirmedInboxMessage(Guid OrderId) : IInboxMessage;
