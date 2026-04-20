using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Sample.TransactionalOutbox.Domain.Inbox;
using Sample.TransactionalOutbox.Persistence.Repository;

namespace Sample.TransactionalOutbox.Persistence.Tests.Repository;

public sealed class InboxMessageRepositoryTests
{
    private static ShopDbContext CreateContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<ShopDbContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new ShopDbContext(options);
    }

    // ──────────────────────────────────────────────
    // Task 3.3 — Property 1: Inbox message persistence round-trip
    // Feature: endpoint-restructure-and-generic-inbox, Property 1: Inbox message persistence round-trip
    // ──────────────────────────────────────────────

    /// <summary>
    /// **Validates: Requirements 4.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public void Property1_InboxMessagePersistenceRoundTrip(Guid id, NonEmptyString messageType, NonEmptyString payload)
    {
        var messageTypeStr = messageType.Get;
        var payloadStr = payload.Get;

        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);
        IInboxMessageRepository repository = new InboxMessageRepository(context);

        // Act
        var result = repository.ReceiveAsync(id, messageTypeStr, payloadStr, CancellationToken.None)
            .GetAwaiter().GetResult();

        // Assert — ReceiveAsync should return true for a new message
        result.Should().BeTrue();

        // Read back the entity using a fresh context to avoid caching
        using var readContext = CreateContext(dbName);
        var entity = readContext.InboxMessages
            .AsNoTracking()
            .SingleOrDefault(m => m.Id == id);

        entity.Should().NotBeNull();
        entity!.Id.Should().Be(id);
        entity.MessageType.Should().Be(messageTypeStr);
        entity.Payload.Should().Be(payloadStr);
        entity.ReceivedAt.Should().NotBe(default);
    }

    // ──────────────────────────────────────────────
    // Task 3.4 — Property 2: Inbox message receive idempotency
    // Feature: endpoint-restructure-and-generic-inbox, Property 2: Inbox message receive idempotency
    // ──────────────────────────────────────────────

    /// <summary>
    /// **Validates: Requirements 4.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public void Property2_InboxMessageReceiveIdempotency(Guid id, NonEmptyString messageType, NonEmptyString payload)
    {
        var messageTypeStr = messageType.Get;
        var payloadStr = payload.Get;

        var dbName = Guid.NewGuid().ToString();

        // First call — should persist and return true
        using (var context1 = CreateContext(dbName))
        {
            IInboxMessageRepository repository1 = new InboxMessageRepository(context1);
            var firstResult = repository1.ReceiveAsync(id, messageTypeStr, payloadStr, CancellationToken.None)
                .GetAwaiter().GetResult();
            firstResult.Should().BeTrue();
        }

        // Second call with a fresh context — should detect duplicate and return false
        using (var context2 = CreateContext(dbName))
        {
            IInboxMessageRepository repository2 = new InboxMessageRepository(context2);
            var secondResult = repository2.ReceiveAsync(id, messageTypeStr, payloadStr, CancellationToken.None)
                .GetAwaiter().GetResult();
            secondResult.Should().BeFalse();
        }

        // Verify exactly one entity exists with that id
        using var verifyContext = CreateContext(dbName);
        var count = verifyContext.InboxMessages
            .AsNoTracking()
            .Count(m => m.Id == id);
        count.Should().Be(1);
    }
}
