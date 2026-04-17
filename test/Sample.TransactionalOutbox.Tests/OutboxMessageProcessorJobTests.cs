using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;
using Sample.TransactionalOutbox.Domain;
using Sample.TransactionalOutbox.Domain.Order.DomainEvents;
using Sample.TransactionalOutbox.Domain.Primitives;
using Sample.TransactionalOutbox.Job;
using Sample.TransactionalOutbox.Persistence;
using Xunit;

namespace Sample.TransactionalOutbox.Tests;

public sealed class OutboxMessageProcessorJobTests
{
    private readonly IPublisher _publisher;
    private readonly ILogger<OutboxMessageProcessorJob> _logger;
    private readonly IJobExecutionContext _jobContext;

    public OutboxMessageProcessorJobTests()
    {
        _publisher = Substitute.For<IPublisher>();
        _logger = Substitute.For<ILogger<OutboxMessageProcessorJob>>();
        _jobContext = Substitute.For<IJobExecutionContext>();
        _jobContext.CancellationToken.Returns(CancellationToken.None);
    }

    private static ShopDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ShopDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ShopDbContext(options);
    }

    private static OutboxMessageEntity CreateValidOutboxMessage(Guid? productId = null)
    {
        var id = productId ?? Guid.NewGuid();
        var domainEvent = new OrderConfirmed(id);
        var content = JsonConvert.SerializeObject(
            domainEvent,
            new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });

        return new OutboxMessageEntity
        {
            Id = Guid.NewGuid(),
            Type = nameof(OrderConfirmed),
            Content = content,
            CreationTime = DateTime.UtcNow,
            CompleteTime = null,
            Exception = null
        };
    }

    // ──────────────────────────────────────────────
    // Task 6.1 — Unit tests
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Execute_WithUnprocessedMessages_DeserializesAndPublishes()
    {
        // Arrange
        using var context = CreateContext();
        var msg1 = CreateValidOutboxMessage();
        var msg2 = CreateValidOutboxMessage();
        context.DomainEvents.AddRange(msg1, msg2);
        await context.SaveChangesAsync();

        var job = new OutboxMessageProcessorJob(_logger, context, _publisher);

        // Act
        await job.Execute(_jobContext);

        // Assert
        await _publisher.Received(2).Publish(
            Arg.Any<IDomainEvent>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WithSuccessfulMessages_RemovesFromTable()
    {
        // Arrange
        using var context = CreateContext();
        var msg = CreateValidOutboxMessage();
        context.DomainEvents.Add(msg);
        await context.SaveChangesAsync();

        var job = new OutboxMessageProcessorJob(_logger, context, _publisher);

        // Act
        await job.Execute(_jobContext);

        // Assert
        var remaining = await context.DomainEvents.ToListAsync();
        remaining.Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_WithDeserializationFailure_SetsExceptionAndCompleteTime()
    {
        // Arrange
        using var context = CreateContext();
        var msg = new OutboxMessageEntity
        {
            Id = Guid.NewGuid(),
            Type = "InvalidType",
            Content = "{ this is not valid json for deserialization }",
            CreationTime = DateTime.UtcNow,
            CompleteTime = null,
            Exception = null
        };
        context.DomainEvents.Add(msg);
        await context.SaveChangesAsync();

        var job = new OutboxMessageProcessorJob(_logger, context, _publisher);

        // Act
        await job.Execute(_jobContext);

        // Assert
        var updated = await context.DomainEvents.SingleAsync();
        updated.Exception.Should().NotBeNullOrWhiteSpace();
        updated.CompleteTime.Should().NotBeNull();
    }

    [Fact]
    public async Task Execute_WithPublishFailure_SetsExceptionAndKeepsMessage()
    {
        // Arrange
        using var context = CreateContext();
        var msg = CreateValidOutboxMessage();
        context.DomainEvents.Add(msg);
        await context.SaveChangesAsync();

        _publisher.Publish(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Publish failed"));

        var job = new OutboxMessageProcessorJob(_logger, context, _publisher);

        // Act
        await job.Execute(_jobContext);

        // Assert
        var updated = await context.DomainEvents.SingleAsync();
        updated.Exception.Should().NotBeNullOrWhiteSpace();
        updated.Exception.Should().Contain("Publish failed");
    }

    [Fact]
    public async Task Execute_WithNoUnprocessedMessages_DoesNotPublish()
    {
        // Arrange
        using var context = CreateContext();
        // Add a message that is already processed (CompleteTime is set)
        var msg = CreateValidOutboxMessage();
        msg.CompleteTime = DateTime.UtcNow;
        context.DomainEvents.Add(msg);
        await context.SaveChangesAsync();

        var job = new OutboxMessageProcessorJob(_logger, context, _publisher);

        // Act
        await job.Execute(_jobContext);

        // Assert
        await _publisher.DidNotReceive().Publish(
            Arg.Any<IDomainEvent>(),
            Arg.Any<CancellationToken>());
    }

    // ──────────────────────────────────────────────
    // Task 6.2 — Property 12: Successful message processing round-trip
    // Validates: Requirements 6.1, 6.2
    // ──────────────────────────────────────────────

    /// <summary>
    /// **Validates: Requirements 6.1, 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public void Property12_SuccessfulMessageProcessingRoundTrip(PositiveInt count)
    {
        var n = Math.Min(count.Get, 10);

        using var context = CreateContext();
        var publisher = Substitute.For<IPublisher>();
        var logger = Substitute.For<ILogger<OutboxMessageProcessorJob>>();
        var jobContext = Substitute.For<IJobExecutionContext>();
        jobContext.CancellationToken.Returns(CancellationToken.None);

        // Arrange — seed N valid unprocessed outbox messages
        var messages = Enumerable.Range(0, n)
            .Select(_ => CreateValidOutboxMessage())
            .ToList();

        context.DomainEvents.AddRange(messages);
        context.SaveChangesAsync().GetAwaiter().GetResult();

        var job = new OutboxMessageProcessorJob(logger, context, publisher);

        // Act
        job.Execute(jobContext).GetAwaiter().GetResult();

        // Assert — publisher was called for each message
        publisher.Received(n).Publish(
            Arg.Any<IDomainEvent>(),
            Arg.Any<CancellationToken>());

        // Assert — all successfully processed messages are removed
        var remaining = context.DomainEvents.ToList();
        remaining.Should().BeEmpty();
    }
}
