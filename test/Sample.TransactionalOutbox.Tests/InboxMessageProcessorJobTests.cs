using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;
using Sample.TransactionalOutbox.Domain;
using Sample.TransactionalOutbox.Domain.Inbox;
using Sample.TransactionalOutbox.Domain.Primitives;
using Sample.TransactionalOutbox.Job;
using Sample.TransactionalOutbox.Persistence;
using Xunit;

namespace Sample.TransactionalOutbox.Tests;

public sealed class InboxMessageProcessorJobTests
{
    private readonly IPublisher _publisher;
    private readonly ILogger<InboxMessageProcessorJob> _logger;
    private readonly IJobExecutionContext _jobContext;
    private readonly MessageTypeRegistry _registry;

    public InboxMessageProcessorJobTests()
    {
        _publisher = Substitute.For<IPublisher>();
        _logger = Substitute.For<ILogger<InboxMessageProcessorJob>>();
        _jobContext = Substitute.For<IJobExecutionContext>();
        _jobContext.CancellationToken.Returns(CancellationToken.None);
        _registry = new MessageTypeRegistry();
        _registry.Register<PaymentConfirmedInboxMessage>("PaymentConfirmed");
    }

    private static ShopDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ShopDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ShopDbContext(options);
    }

    private static InboxMessageEntity CreatePaymentConfirmedMessage(Guid orderId, bool alreadyProcessed = false)
    {
        var payload = JsonConvert.SerializeObject(new { OrderId = orderId });
        return new InboxMessageEntity
        {
            Id = Guid.NewGuid(),
            MessageType = "PaymentConfirmed",
            Payload = payload,
            ReceivedAt = DateTime.UtcNow,
            ProcessedAt = alreadyProcessed ? DateTime.UtcNow : null,
            Error = null
        };
    }

    // ──────────────────────────────────────────────
    // Unit tests for InboxMessageProcessorJob
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Execute_PaymentConfirmed_SetsProcessedAtAndPublishesMessage()
    {
        // Arrange
        using var context = CreateContext();
        var orderId = Guid.NewGuid();
        var message = CreatePaymentConfirmedMessage(orderId);
        context.InboxMessages.Add(message);
        await context.SaveChangesAsync();

        var job = new InboxMessageProcessorJob(_logger, context, _publisher, _registry);

        // Act
        await job.Execute(_jobContext);

        // Assert
        var updated = await context.InboxMessages.SingleAsync();
        updated.ProcessedAt.Should().NotBeNull();
        updated.Error.Should().BeNull();
        await _publisher.Received(1).Publish(
            Arg.Is<PaymentConfirmedInboxMessage>(m => m.OrderId == orderId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_AlreadyProcessedMessage_IsSkipped()
    {
        // Arrange
        using var context = CreateContext();
        var orderId = Guid.NewGuid();
        var message = CreatePaymentConfirmedMessage(orderId, alreadyProcessed: true);
        var originalProcessedAt = message.ProcessedAt;
        context.InboxMessages.Add(message);
        await context.SaveChangesAsync();

        var job = new InboxMessageProcessorJob(_logger, context, _publisher, _registry);

        // Act
        await job.Execute(_jobContext);

        // Assert — publisher should never be called for already-processed messages
        await _publisher.DidNotReceive().Publish(Arg.Any<IInboxMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenPublishThrows_SetsErrorAndProcessedAt()
    {
        // Arrange
        using var context = CreateContext();
        var orderId = Guid.NewGuid();
        var message = CreatePaymentConfirmedMessage(orderId);
        context.InboxMessages.Add(message);
        await context.SaveChangesAsync();

        _publisher.Publish(Arg.Any<PaymentConfirmedInboxMessage>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Order not found"));

        var job = new InboxMessageProcessorJob(_logger, context, _publisher, _registry);

        // Act
        await job.Execute(_jobContext);

        // Assert
        var updated = await context.InboxMessages.SingleAsync();
        updated.ProcessedAt.Should().NotBeNull();
        updated.Error.Should().NotBeNullOrWhiteSpace();
        updated.Error.Should().Contain("Order not found");
    }

    [Fact]
    public async Task Execute_WithNoUnprocessedMessages_DoesNothing()
    {
        // Arrange
        using var context = CreateContext();
        // Empty inbox — no messages at all
        var job = new InboxMessageProcessorJob(_logger, context, _publisher, _registry);

        // Act
        await job.Execute(_jobContext);

        // Assert — publisher should never be called
        await _publisher.DidNotReceive().Publish(Arg.Any<IInboxMessage>(), Arg.Any<CancellationToken>());
    }

    // ──────────────────────────────────────────────
    // Edge case tests (Req 10.4, 10.9, 11.4)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Execute_UnknownMessageType_SetsProcessedAtWithNoError()
    {
        // Arrange — Req 10.4: unknown MessageType logs warning and marks processed
        using var context = CreateContext();
        var message = new InboxMessageEntity
        {
            Id = Guid.NewGuid(),
            MessageType = "UnknownType",
            Payload = "{}",
            ReceivedAt = DateTime.UtcNow,
            ProcessedAt = null,
            Error = null
        };
        context.InboxMessages.Add(message);
        await context.SaveChangesAsync();

        var job = new InboxMessageProcessorJob(_logger, context, _publisher, _registry);

        // Act
        await job.Execute(_jobContext);

        // Assert — ProcessedAt is set, Error remains null, publisher never called
        var updated = await context.InboxMessages.SingleAsync();
        updated.ProcessedAt.Should().NotBeNull();
        updated.Error.Should().BeNull();
        await _publisher.DidNotReceive().Publish(Arg.Any<IInboxMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_BatchLimitOf10_ProcessesOnly10Messages()
    {
        // Arrange — Req 10.9: job processes at most 10 messages per execution
        using var context = CreateContext();
        for (var i = 0; i < 15; i++)
        {
            context.InboxMessages.Add(CreatePaymentConfirmedMessage(Guid.NewGuid()));
        }
        await context.SaveChangesAsync();

        var job = new InboxMessageProcessorJob(_logger, context, _publisher, _registry);

        // Act
        await job.Execute(_jobContext);

        // Assert — exactly 10 processed, 5 remain unprocessed
        var all = await context.InboxMessages.ToListAsync();
        all.Should().HaveCount(15);
        all.Count(m => m.ProcessedAt != null).Should().Be(10);
        all.Count(m => m.ProcessedAt == null).Should().Be(5);
    }

    [Fact]
    public async Task Execute_PaymentConfirmedWithEmptyOrderId_CapturesInvalidOperationException()
    {
        // Arrange — Req 11.4: PaymentConfirmedHandler throws InvalidOperationException for empty OrderId.
        // The handler is internal sealed, so we test indirectly through the job.
        // Configure the mock publisher to throw when it receives a message with Guid.Empty,
        // simulating the handler's validation behavior.
        using var context = CreateContext();
        var payload = JsonConvert.SerializeObject(new { OrderId = Guid.Empty });
        var message = new InboxMessageEntity
        {
            Id = Guid.NewGuid(),
            MessageType = "PaymentConfirmed",
            Payload = payload,
            ReceivedAt = DateTime.UtcNow,
            ProcessedAt = null,
            Error = null
        };
        context.InboxMessages.Add(message);
        await context.SaveChangesAsync();

        _publisher.Publish(
            Arg.Is<PaymentConfirmedInboxMessage>(m => m.OrderId == Guid.Empty),
            Arg.Any<CancellationToken>()
        ).ThrowsAsync(new InvalidOperationException("OrderId cannot be empty."));

        var job = new InboxMessageProcessorJob(_logger, context, _publisher, _registry);

        // Act
        await job.Execute(_jobContext);

        // Assert — error is captured, ProcessedAt is set
        var updated = await context.InboxMessages.SingleAsync();
        updated.ProcessedAt.Should().NotBeNull();
        updated.Error.Should().NotBeNullOrWhiteSpace();
        updated.Error.Should().Contain("OrderId cannot be empty.");
    }
}
