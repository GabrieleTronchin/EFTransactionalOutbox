using Sample.TransactionalOutbox.Domain.Primitives;

namespace Sample.TransactionalOutbox.Domain.Inbox;

public class MessageTypeRegistry
{
    private readonly Dictionary<string, Type> _mappings = new();

    public MessageTypeRegistry Register<T>(string messageType) where T : IInboxMessage
    {
        _mappings[messageType] = typeof(T);
        return this;
    }

    public Type? Resolve(string messageType)
    {
        _mappings.TryGetValue(messageType, out var type);
        return type;
    }
}
