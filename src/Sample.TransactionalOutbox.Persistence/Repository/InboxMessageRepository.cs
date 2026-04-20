using Microsoft.EntityFrameworkCore;
using Sample.TransactionalOutbox.Domain;
using Sample.TransactionalOutbox.Domain.Inbox;

namespace Sample.TransactionalOutbox.Persistence.Repository;

internal class InboxMessageRepository : IInboxMessageRepository
{
    private readonly ShopDbContext _context;

    public InboxMessageRepository(ShopDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ReceiveAsync(Guid id, string messageType, string payload, CancellationToken cancellationToken)
    {
        var exists = await _context.InboxMessages
            .AsNoTracking()
            .AnyAsync(m => m.Id == id, cancellationToken);

        if (exists)
        {
            return false;
        }

        var entity = new InboxMessageEntity
        {
            Id = id,
            MessageType = messageType,
            Payload = payload,
            ReceivedAt = DateTime.UtcNow
        };

        await _context.InboxMessages.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
