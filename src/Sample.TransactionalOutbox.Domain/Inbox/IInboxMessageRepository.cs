namespace Sample.TransactionalOutbox.Domain.Inbox;

public interface IInboxMessageRepository
{
    /// <summary>
    /// Receives an inbox message. Returns true if the message was persisted (new),
    /// false if it was a duplicate.
    /// </summary>
    Task<bool> ReceiveAsync(Guid id, string messageType, string payload, CancellationToken cancellationToken);
}
