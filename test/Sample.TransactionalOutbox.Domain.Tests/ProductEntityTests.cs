using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Sample.TransactionalOutbox.Domain.Product;
using Sample.TransactionalOutbox.Domain.Tests.Generators;
using Xunit;

namespace Sample.TransactionalOutbox.Domain.Tests;

public sealed class ProductEntityTests
{
    // ── Unit Tests (Task 4.1) ──────────────────────────────────────────

    [Fact]
    public void Create_WithPositiveQuantity_ReturnsProductWithCorrectQuantity()
    {
        // Arrange
        var quantity = 5;

        // Act
        var product = ProductEntity.Create(quantity);

        // Assert
        product.Quantity.Should().Be(quantity);
        product.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_WithZeroQuantity_ThrowsException()
    {
        // Act
        var act = () => ProductEntity.Create(0);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithNegativeQuantity_ThrowsException()
    {
        // Act
        var act = () => ProductEntity.Create(-1);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void HasBeenConfirmed_WithPositiveQuantity_DecrementsQuantityByOne()
    {
        // Arrange
        var product = ProductEntity.Create(3);

        // Act
        product.HasBeenConfirmed();

        // Assert
        product.Quantity.Should().Be(2);
    }

    [Fact]
    public void HasBeenConfirmed_WithZeroQuantity_ThrowsInvalidOperationException()
    {
        // Arrange
        var product = ProductEntity.Create(1);
        product.HasBeenConfirmed();

        // Act
        var act = () => product.HasBeenConfirmed();

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    // ── Property Tests ─────────────────────────────────────────────────

    /// <summary>
    /// Property 7: ProductEntity.Create preserves quantity.
    /// For any positive integer, Create produces an instance where Quantity equals the input.
    /// </summary>
    /// <remarks>Validates: Requirements 4.1</remarks>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(DomainGenerators) })]
    public void Property7_CreatePreservesQuantity(PositiveInt positiveQuantity)
    {
        // Arrange
        var quantity = positiveQuantity.Get;

        // Act
        var product = ProductEntity.Create(quantity);

        // Assert
        product.Quantity.Should().Be(quantity);
    }

    /// <summary>
    /// Property 8: ProductEntity.Create rejects non-positive quantities.
    /// For any integer ≤ 0, Create throws an exception.
    /// </summary>
    /// <remarks>Validates: Requirements 4.2</remarks>
    [Property(MaxTest = 100)]
    public void Property8_CreateRejectsNonPositiveQuantities(int value)
    {
        // Arrange — constrain to non-positive values (≤ 0)
        var quantity = -Math.Abs(value);

        // Act
        var act = () => ProductEntity.Create(quantity);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Property 9: HasBeenConfirmed decrement invariance.
    /// For any product with initial quantity Q > 0 and N calls (1 ≤ N ≤ Q),
    /// Quantity equals Q − N.
    /// </summary>
    /// <remarks>Validates: Requirements 4.3, 4.5</remarks>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(DomainGenerators) })]
    public void Property9_HasBeenConfirmedDecrementInvariance(PositiveInt positiveQuantity)
    {
        // Arrange
        var q = positiveQuantity.Get;
        var n = (q == 1) ? 1 : new Random().Next(1, q + 1);
        var product = ProductEntity.Create(q);

        // Act
        for (var i = 0; i < n; i++)
        {
            product.HasBeenConfirmed();
        }

        // Assert
        product.Quantity.Should().Be(q - n);
    }
}
