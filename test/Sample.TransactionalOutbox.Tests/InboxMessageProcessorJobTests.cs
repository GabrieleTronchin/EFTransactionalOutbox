using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;
using Sample.TransactionalOutbox.Domain;
using Sample.TransactionalOutbox.Domain.Order;
using Sample.TransactionalOutbox.Job;
using Sample.TransactionalOutbox.Persistence;
using Xunit;

namespace Sample.TransactionalOutbox.Tests;

public sealed class InboxMessageProcessorJobTests
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<InboxMessageProcessorJob> _logger;
    private readonly IJobExecutionContext _jobContext;

    public InboxMessageProcessorJobTests()
    {
        _orderRepository = Substitute.For<IOrderRepository>();
        _logger = Substitute.For<ILogger<InboxMessageProcessorJob>>();
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

    private static OrderEntity CreatePendingOrder(Guid productId)
    {
        return OrderEntity.Create(
            productId,
            quantity: 2,
            totalAmount: 49.99m,
            customerName: "Test Customer",
            shippingAddress: "123 Test St");
    }

    // ──────────────────────────────────────────────
    // Task 6.3 — Unit tests for InboxMessageProcessorJob
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Execute_PaymentConfirmed_SetsProcessedAtAndConfirmsOrder()
    {
        // Arrange
        using var context = CreateContext();
        var orderId = Guid.NewGuid();
        var order = CreatePendingOrder(Guid.NewGuid());

        // We need the order's actual Id for the lookup
        var orderIdField = typeof(OrderEntity).GetProperty("Id")!;
        // OrderEntity.Create generates a random Id, so we use that
        var actualOrderId = order.Id;

        var message = CreatePaymentConfirmedMessage(actualOrderId);
        context.InboxMessages.Add(message);
        await context.SaveChangesAsync();

        _orderRepository.GetAsync(actualOrderId, Arg.Any<CancellationToken>())
            .Returns(order);

        var job = new InboxMessageProcessorJob(_logger, context, _orderRepository);

        // Act
        await job.Execute(_jobContext);

        // Assert
        var updated = await context.InboxMessages.SingleAsync();
        updated.ProcessedAt.Should().NotBeNull();
        updated.Error.Should().BeNull();
        order.OrderStatus.Should().Be(OrderStatus.Confirmed);
        order.ConfirmedAt.Should().NotBeNull();
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

        var job = new InboxMessageProcessorJob(_logger, context, _orderRepository);

        // Act
        await job.Execute(_jobContext);

        // Assert — repository should never be called for already-processed messages
        await _orderRepository.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenHandlerThrows_SetsErrorAndProcessedAt()
    {
        // Arrange
        using var context = CreateContext();
        var orderId = Guid.NewGuid();
        var message = CreatePaymentConfirmedMessage(orderId);
        context.InboxMessages.Add(message);
        await context.SaveChangesAsync();

        _orderRepository.GetAsync(orderId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Order not found"));

        var job = new InboxMessageProcessorJob(_logger, context, _orderRepository);

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
        var job = new InboxMessageProcessorJob(_logger, context, _orderRepository);

        // Act
        await job.Execute(_jobContext);

        // Assert — repository should never be called
        await _orderRepository.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
