namespace Sample.TransactionalOutbox.Contracts;

public record InboxReceiveRequest(Guid Id, string MessageType, string Payload);
