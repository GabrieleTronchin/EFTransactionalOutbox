using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Sample.TransactionalOutbox.Domain.Order;
using Sample.TransactionalOutbox.Domain.Order.DomainEvents;
using Sample.TransactionalOutbox.Persistence.Interceptors;
using Xunit;

namespace Sample.TransactionalOutbox.Persistence.Tests;

public sealed class OrderDomainEventInterceptorTests
{
    private static ShopDbContext CreateContext()
    {
        var interceptor = new OrderDomainEventInterceptor();
        var options = new DbContextOptionsBuilder<ShopDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;
        return new ShopDbContext(options);
    }

    // ──────────────────────────────────────────────
    // Task 5.1 — Unit tests
    // ──────────────────────────────────────────────

    [Fact]
    public async Task SavingChangesAsync_WithOrderEvents_CreatesOutboxMessages()
    {
        // Arrange
        using var context = CreateContext();
        var order1 = OrderEntity.Create(Guid.NewGuid(), 1, 10.00m, "Alice Smith");
        var order2 = OrderEntity.Create(Guid.NewGuid(), 2, 25.00m, "Bob Jones");
        order1.ConfirmPayment();
        order2.ConfirmPayment();
        context.Orders.AddRange(order1, order2);

        // Act
        await context.SaveChangesAsync();

        // Assert
        var outboxMessages = await context.DomainEvents.ToListAsync();
        outboxMessages.Should().HaveCount(2);
    }

    [Fact]
    public async Task SavingChangesAsync_WithOrderEvents_SetsCorrectTypeField()
    {
        // Arrange
        using var context = CreateContext();
        var order = OrderEntity.Create(Guid.NewGuid(), 1, 10.00m, "Test Customer");
        order.ConfirmPayment();
        context.Orders.Add(order);

        // Act
        await context.SaveChangesAsync();

        // Assert
        var outboxMessage = await context.DomainEvents.SingleAsync();
        outboxMessage.Type.Should().Be(nameof(OrderConfirmed));
    }

    [Fact]
    public async Task SavingChangesAsync_WithOrderEvents_SerializesContentAsJson()
    {
        // Arrange
        using var context = CreateContext();
        var productId = Guid.NewGuid();
        var order = OrderEntity.Create(productId, 1, 10.00m, "Test Customer");
        order.ConfirmPayment();
        context.Orders.Add(order);

        // Act
        await context.SaveChangesAsync();

        // Assert
        var outboxMessage = await context.DomainEvents.SingleAsync();
        outboxMessage.Content.Should().NotBeNullOrWhiteSpace();

        var deserialized = JsonConvert.DeserializeObject<OrderConfirmed>(
            outboxMessage.Content,
            new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });

        deserialized.Should().NotBeNull();
        deserialized!.ProductId.Should().Be(productId);
    }

    [Fact]
    public async Task SavingChangesAsync_WithOrderEvents_ClearsEventsFromEntity()
    {
        // Arrange
        using var context = CreateContext();
        var order = OrderEntity.Create(Guid.NewGuid(), 1, 10.00m, "Test Customer");
        order.ConfirmPayment();
        context.Orders.Add(order);

        // Act
        await context.SaveChangesAsync();

        // Assert
        order.GetEvents().Should().BeEmpty();
    }

    [Fact]
    public async Task SavingChangesAsync_WithNoEvents_DoesNotCreateOutboxMessages()
    {
        // Arrange
        using var context = CreateContext();
        var order = OrderEntity.Create(Guid.NewGuid(), 1, 10.00m, "Test Customer");
        context.Orders.Add(order);

        // Act
        await context.SaveChangesAsync();

        // Assert
        var outboxMessages = await context.DomainEvents.ToListAsync();
        outboxMessages.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────
    // Task 5.2 — Property 10: Interceptor creates correct outbox messages
    // Validates: Requirements 5.1, 5.2, 5.3
    // ──────────────────────────────────────────────

    /// <summary>
    /// **Validates: Requirements 5.1, 5.2, 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public void Property10_InterceptorCreatesCorrectOutboxMessages(PositiveInt count)
    {
        var n = Math.Min(count.Get, 20);

        using var context = CreateContext();

        // Create N orders, each confirmed (producing one OrderConfirmed event each)
        var orders = Enumerable.Range(0, n)
            .Select(_ =>
            {
                var order = OrderEntity.Create(Guid.NewGuid(), 1, 10.00m, "Property Test Customer");
                order.ConfirmPayment();
                return order;
            })
            .ToList();

        context.Orders.AddRange(orders);
        context.SaveChangesAsync().GetAwaiter().GetResult();

        var outboxMessages = context.DomainEvents.ToList();

        // Exactly N outbox messages
        outboxMessages.Should().HaveCount(n);

        // All have correct Type
        outboxMessages.Should().AllSatisfy(m =>
            m.Type.Should().Be(nameof(OrderConfirmed)));

        // All Content round-trips correctly
        outboxMessages.Should().AllSatisfy(m =>
        {
            var deserialized = JsonConvert.DeserializeObject<OrderConfirmed>(
                m.Content,
                new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
            deserialized.Should().NotBeNull();
        });
    }

    // ──────────────────────────────────────────────
    // Task 5.3 — Property 11: Interceptor clears events after persistence
    // Validates: Requirements 5.4
    // ──────────────────────────────────────────────

    /// <summary>
    /// **Validates: Requirements 5.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public void Property11_InterceptorClearsEventsAfterPersistence(PositiveInt count)
    {
        var n = Math.Min(count.Get, 20);

        using var context = CreateContext();

        var orders = Enumerable.Range(0, n)
            .Select(_ =>
            {
                var order = OrderEntity.Create(Guid.NewGuid(), 1, 10.00m, "Property Test Customer");
                order.ConfirmPayment();
                return order;
            })
            .ToList();

        context.Orders.AddRange(orders);
        context.SaveChangesAsync().GetAwaiter().GetResult();

        // After save, all orders should have empty event lists
        orders.Should().AllSatisfy(o =>
            o.GetEvents().Should().BeEmpty());
    }
}
