namespace Sample.TransactionalOutbox.Domain
{
    public sealed class InboxMessageEntity
    {
        public Guid Id { get; set; }

        public string MessageType { get; set; } = string.Empty;

        public string Payload { get; set; } = string.Empty;

        public DateTime ReceivedAt { get; set; }

        public DateTime? ProcessedAt { get; set; }

        public string? Error { get; set; }
    }
}
