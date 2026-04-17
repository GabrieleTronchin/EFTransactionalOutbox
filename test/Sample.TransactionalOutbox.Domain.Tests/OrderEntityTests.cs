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
    [Fact]
    public void Create_WithValidInputs_ReturnsUnconfirmedOrder()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var description = "Test order description";

        // Act
        var order = OrderEntity.Create(productId, description);

        // Assert
        order.Confirmed.Should().BeFalse();
        order.ProductId.Should().Be(productId);
        order.Description.Should().Be(description);
    }

    [Fact]
    public void Create_WithNullDescription_ThrowsArgumentException()
    {
        // Arrange
        var productId = Guid.NewGuid();

        // Act
        var act = () => OrderEntity.Create(productId, null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithEmptyDescription_ThrowsArgumentException()
    {
        // Arrange
        var productId = Guid.NewGuid();

        // Act
        var act = () => OrderEntity.Create(productId, string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithWhitespaceDescription_ThrowsArgumentException()
    {
        // Arrange
        var productId = Guid.NewGuid();

        // Act
        var act = () => OrderEntity.Create(productId, "   ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ConfirmPayment_OnUnconfirmedOrder_SetsConfirmedTrue()
    {
        // Arrange
        var order = OrderEntity.Create(Guid.NewGuid(), "Test order");

        // Act
        order.ConfirmPayment();

        // Assert
        order.Confirmed.Should().BeTrue();
    }

    [Fact]
    public void ConfirmPayment_OnUnconfirmedOrder_RaisesOrderConfirmedEvent()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var order = OrderEntity.Create(productId, "Test order");

        // Act
        order.ConfirmPayment();

        // Assert
        var events = order.GetEvents().ToList();
        events.Should().ContainSingle()
            .Which.Should().BeOfType<OrderConfirmed>()
            .Which.productId.Should().Be(productId);
    }

    [Fact]
    public void ConfirmPayment_OnConfirmedOrder_ThrowsInvalidOperationException()
    {
        // Arrange
        var order = OrderEntity.Create(Guid.NewGuid(), "Test order");
        order.ConfirmPayment();

        // Act
        var act = () => order.ConfirmPayment();

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Property 3: OrderEntity.Create invariant — new orders are unconfirmed.
    /// For any valid productId and non-empty description, Create produces an instance
    /// with Confirmed=false, matching ProductId and Description.
    /// </summary>
    /// <remarks>Validates: Requirements 3.1</remarks>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(DomainGenerators) })]
    public void Property3_CreateInvariant_NewOrdersAreUnconfirmed(Guid productId, string description)
    {
        // Act
        var order = OrderEntity.Create(productId, description);

        // Assert
        order.Confirmed.Should().BeFalse();
        order.ProductId.Should().Be(productId);
        order.Description.Should().Be(description);
    }

    /// <summary>
    /// Property 4: OrderEntity.Create rejects invalid descriptions.
    /// For any null, empty, or whitespace-only string, Create throws ArgumentException.
    /// </summary>
    /// <remarks>Validates: Requirements 3.2</remarks>
    [Property(MaxTest = 100)]
    public void Property4_CreateRejectsInvalidDescriptions(byte whitespaceLength)
    {
        // Generate null, empty, or whitespace-only strings
        var invalidDescriptions = new[]
        {
            null!,
            string.Empty,
            new string(' ', Math.Max(1, whitespaceLength % 20 + 1)),
            new string('\t', Math.Max(1, whitespaceLength % 10 + 1)),
            new string('\n', Math.Max(1, whitespaceLength % 10 + 1)),
        };

        foreach (var description in invalidDescriptions)
        {
            // Act
            var act = () => OrderEntity.Create(Guid.NewGuid(), description);

            // Assert
            act.Should().Throw<ArgumentException>();
        }
    }

    /// <summary>
    /// Property 5: ConfirmPayment sets Confirmed and raises correct event.
    /// For any unconfirmed OrderEntity, ConfirmPayment sets Confirmed=true and adds
    /// exactly one OrderConfirmed event with matching ProductId.
    /// </summary>
    /// <remarks>Validates: Requirements 3.3, 3.4</remarks>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(DomainGenerators) })]
    public void Property5_ConfirmPaymentSetsConfirmedAndRaisesCorrectEvent(Guid productId, string description)
    {
        // Arrange
        var order = OrderEntity.Create(productId, description);

        // Act
        order.ConfirmPayment();

        // Assert
        order.Confirmed.Should().BeTrue();

        var events = order.GetEvents().ToList();
        events.Should().ContainSingle()
            .Which.Should().BeOfType<OrderConfirmed>()
            .Which.productId.Should().Be(productId);
    }

    /// <summary>
    /// Property 6: Double ConfirmPayment throws.
    /// For any already-confirmed OrderEntity, calling ConfirmPayment again throws InvalidOperationException.
    /// </summary>
    /// <remarks>Validates: Requirements 3.5</remarks>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(DomainGenerators) })]
    public void Property6_DoubleConfirmPaymentThrows(Guid productId, string description)
    {
        // Arrange
        var order = OrderEntity.Create(productId, description);
        order.ConfirmPayment();

        // Act
        var act = () => order.ConfirmPayment();

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }
}
