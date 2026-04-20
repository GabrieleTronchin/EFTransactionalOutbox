using MediatR;

namespace Sample.TransactionalOutbox.Domain.Primitives;

public interface IInboxMessage : INotification { }
