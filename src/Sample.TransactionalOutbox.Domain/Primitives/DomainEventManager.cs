namespace Sample.TransactionalOutbox.Domain.Primitives;

public abstract class DomainEventManager
{
    private readonly IList<IDomainEvent> _events;

    protected DomainEventManager()
    {
        _events = new List<IDomainEvent>();
    }

    public void RaiseEvent(IDomainEvent domainEvent)
    {
        _events.Add(domainEvent);
    }

    public IEnumerable<IDomainEvent> GetEvents()
    {
        return _events.ToList();
    }

    public void ClearEvents()
    {
        _events.Clear();
    }
}
