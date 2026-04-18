using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Sample.TransactionalOutbox.Domain.Order;
using Sample.TransactionalOutbox.Domain.Order.DomainEvents;
using Sample.TransactionalOutbox.Domain.Tests.Generators;
using Xunit;

namespace Sample.TransactionalOutbox.Domain.Tests;

public sealed class OrderEntityTests
{
    // ── Helper to create a valid pending order with defaults ────────────

    private static OrderEntity CreateValid(
        Guid? productId = null,
        int quantity = 2,
        decimal totalAmount = 49.99m,
        string customerName = "Jane Doe",
        string? shippingAddress = null)
        => OrderEntity.Create(productId ?? Guid.NewGuid(), quantity, totalAmount, customerName, shippingAddress);

    // ── Unit Tests: Create ─────────────────────────────────────────────

    [Fact]
    public void Create_WithValidInputs_ReturnsPendingOrder()
    {
        // Arrange
        var productId = Guid.NewGuid();

        // Act
        var order = OrderEntity.Create(productId, 3, 29.97m, "Alice Smith", "123 Main St");

        // Assert
        order.OrderStatus.Should().Be(OrderStatus.Pending);
        order.ProductId.Should().Be(productId);
        order.Quantity.Should().Be(3);
        order.TotalAmount.Should().Be(29.97m);
        order.CustomerName.Should().Be("Alice Smith");
        order.ShippingAddress.Should().Be("123 Main St");
        order.Id.Should().NotBeEmpty();
        order.ConfirmedAt.Should().BeNull();
    }

    [Fact]
    public void Create_SetsCreatedAtToApproximatelyUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var order = CreateValid();

        // Assert
        var after = DateTime.UtcNow;
        order.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Create_WithNullShippingAddress_SetsShippingAddressToNull()
    {
        // Act
        var order = OrderEntity.Create(Guid.NewGuid(), 1, 10m, "Bob");

        // Assert
        order.ShippingAddress.Should().BeNull();
    }

    // ── Validation: CustomerName ───────────────────────────────────────

    [Fact]
    public void Create_WithEmptyCustomerName_ThrowsArgumentException()
    {
        var act = () => OrderEntity.Create(Guid.NewGuid(), 1, 10m, string.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithWhitespaceCustomerName_ThrowsArgumentException()
    {
        var act = () => OrderEntity.Create(Guid.NewGuid(), 1, 10m, "   ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNullCustomerName_ThrowsArgumentException()
    {
        var act = () => OrderEntity.Create(Guid.NewGuid(), 1, 10m, null!);
        act.Should().Throw<ArgumentException>();
    }

    // ── Validation: Quantity ───────────────────────────────────────────

    [Fact]
    public void Create_WithZeroQuantity_ThrowsArgumentException()
    {
        var act = () => OrderEntity.Create(Guid.NewGuid(), 0, 10m, "Alice");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNegativeQuantity_ThrowsArgumentException()
    {
        var act = () => OrderEntity.Create(Guid.NewGuid(), -1, 10m, "Alice");
        act.Should().Throw<ArgumentException>();
    }

    // ── Validation: TotalAmount ────────────────────────────────────────

    [Fact]
    public void Create_WithNegativeTotalAmount_ThrowsArgumentException()
    {
        var act = () => OrderEntity.Create(Guid.NewGuid(), 1, -0.01m, "Alice");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithZeroTotalAmount_DoesNotThrow()
    {
        // Zero total amount is allowed (e.g. free order / promo)
        var order = OrderEntity.Create(Guid.NewGuid(), 1, 0m, "Alice");
        order.TotalAmount.Should().Be(0m);
    }

    // ── Unit Tests: ConfirmPayment ─────────────────────────────────────

    [Fact]
    public void ConfirmPayment_OnPendingOrder_SetsConfirmedStatus()
    {
        // Arrange
        var order = CreateValid();

        // Act
        order.ConfirmPayment();

        // Assert
        order.OrderStatus.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public void ConfirmPayment_OnPendingOrder_SetsConfirmedAt()
    {
        // Arrange
        var order = CreateValid();
        var before = DateTime.UtcNow;

        // Act
        order.ConfirmPayment();

        // Assert
        var after = DateTime.UtcNow;
        order.ConfirmedAt.Should().NotBeNull();
        order.ConfirmedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void ConfirmPayment_OnPendingOrder_RaisesOrderConfirmedEvent()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var order = OrderEntity.Create(productId, 1, 10m, "Alice");

        // Act
        order.ConfirmPayment();

        // Assert
        var events = order.GetEvents().ToList();
        events.Should().ContainSingle()
            .Which.Should().BeOfType<OrderConfirmed>();

        var confirmed = (OrderConfirmed)events.Single();
        confirmed.OrderId.Should().Be(order.Id);
        confirmed.ProductId.Should().Be(productId);
    }

    [Fact]
    public void ConfirmPayment_OnConfirmedOrder_ThrowsInvalidOperationException()
    {
        // Arrange
        var order = CreateValid();
        order.ConfirmPayment();

        // Act
        var act = () => order.ConfirmPayment();

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ConfirmPayment_OnCancelledOrder_ThrowsInvalidOperationException()
    {
        // Arrange
        var order = CreateValid();
        order.CancelOrder();

        // Act
        var act = () => order.ConfirmPayment();

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    // ── Unit Tests: CancelOrder ────────────────────────────────────────

    [Fact]
    public void CancelOrder_OnPendingOrder_SetsCancelledStatus()
    {
        // Arrange
        var order = CreateValid();

        // Act
        order.CancelOrder();

        // Assert
        order.OrderStatus.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void CancelOrder_OnPendingOrder_RaisesOrderCancelledEvent()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var order = OrderEntity.Create(productId, 1, 10m, "Alice");

        // Act
        order.CancelOrder();

        // Assert
        var events = order.GetEvents().ToList();
        events.Should().ContainSingle()
            .Which.Should().BeOfType<OrderCancelled>();

        var cancelled = (OrderCancelled)events.Single();
        cancelled.OrderId.Should().Be(order.Id);
        cancelled.ProductId.Should().Be(productId);
    }

    [Fact]
    public void CancelOrder_OnConfirmedOrder_ThrowsInvalidOperationException()
    {
        // Arrange
        var order = CreateValid();
        order.ConfirmPayment();

        // Act
        var act = () => order.CancelOrder();

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CancelOrder_OnCancelledOrder_ThrowsInvalidOperationException()
    {
        // Arrange
        var order = CreateValid();
        order.CancelOrder();

        // Act
        var act = () => order.CancelOrder();

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    // ── Property Tests ─────────────────────────────────────────────────

    /// <summary>
    /// Property 3: OrderEntity.Create invariant — new orders are Pending.
    /// For any valid productId, quantity, totalAmount, and customerName,
    /// Create produces an instance with OrderStatus=Pending, matching properties,
    /// and ConfirmedAt=null.
    /// </summary>
    /// <remarks>Validates: Requirements 2.1, 2.3</remarks>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(DomainGenerators) })]
    public void Property3_CreateInvariant_NewOrdersArePending(
        Guid productId, PositiveInt positiveQuantity, decimal totalAmount, string customerName)
    {
        // Arrange
        var quantity = positiveQuantity.Get;

        // Act
        var order = OrderEntity.Create(productId, quantity, totalAmount, customerName);

        // Assert
        order.OrderStatus.Should().Be(OrderStatus.Pending);
        order.ProductId.Should().Be(productId);
        order.Quantity.Should().Be(quantity);
        order.TotalAmount.Should().Be(totalAmount);
        order.CustomerName.Should().Be(customerName);
        order.ConfirmedAt.Should().BeNull();
    }

    /// <summary>
    /// Property 4: OrderEntity.Create rejects invalid customerName.
    /// For any null, empty, or whitespace-only string, Create throws ArgumentException.
    /// </summary>
    /// <remarks>Validates: Requirements 2.10</remarks>
    [Property(MaxTest = 100)]
    public void Property4_CreateRejectsInvalidCustomerNames(byte whitespaceLength)
    {
        var invalidNames = new[]
        {
            null!,
            string.Empty,
            new string(' ', Math.Max(1, whitespaceLength % 20 + 1)),
            new string('\t', Math.Max(1, whitespaceLength % 10 + 1)),
            new string('\n', Math.Max(1, whitespaceLength % 10 + 1)),
        };

        foreach (var name in invalidNames)
        {
            var act = () => OrderEntity.Create(Guid.NewGuid(), 1, 10m, name);
            act.Should().Throw<ArgumentException>();
        }
    }

    /// <summary>
    /// Property 5: ConfirmPayment sets Confirmed status and raises correct event.
    /// For any pending OrderEntity, ConfirmPayment sets OrderStatus=Confirmed,
    /// sets ConfirmedAt, and adds exactly one OrderConfirmed event with matching OrderId and ProductId.
    /// </summary>
    /// <remarks>Validates: Requirements 2.5, 2.6</remarks>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(DomainGenerators) })]
    public void Property5_ConfirmPaymentSetsConfirmedAndRaisesCorrectEvent(
        Guid productId, PositiveInt positiveQuantity, decimal totalAmount, string customerName)
    {
        // Arrange
        var order = OrderEntity.Create(productId, positiveQuantity.Get, totalAmount, customerName);

        // Act
        order.ConfirmPayment();

        // Assert
        order.OrderStatus.Should().Be(OrderStatus.Confirmed);
        order.ConfirmedAt.Should().NotBeNull();

        var events = order.GetEvents().ToList();
        events.Should().ContainSingle()
            .Which.Should().BeOfType<OrderConfirmed>();

        var confirmed = (OrderConfirmed)events.Single();
        confirmed.OrderId.Should().Be(order.Id);
        confirmed.ProductId.Should().Be(productId);
    }

    /// <summary>
    /// Property 6: Double ConfirmPayment throws.
    /// For any already-confirmed OrderEntity, calling ConfirmPayment again throws InvalidOperationException.
    /// </summary>
    /// <remarks>Validates: Requirements 2.6</remarks>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(DomainGenerators) })]
    public void Property6_DoubleConfirmPaymentThrows(
        Guid productId, PositiveInt positiveQuantity, decimal totalAmount, string customerName)
    {
        // Arrange
        var order = OrderEntity.Create(productId, positiveQuantity.Get, totalAmount, customerName);
        order.ConfirmPayment();

        // Act
        var act = () => order.ConfirmPayment();

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }
}
