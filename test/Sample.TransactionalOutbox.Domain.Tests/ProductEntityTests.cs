using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Sample.TransactionalOutbox.Domain.Product;
using Sample.TransactionalOutbox.Domain.Tests.Generators;
using Xunit;

namespace Sample.TransactionalOutbox.Domain.Tests;

public sealed class ProductEntityTests
{
    // ── Helper to create a valid product with defaults ──────────────────

    private static ProductEntity CreateValid(
        string name = "Test Product",
        decimal price = 9.99m,
        string sku = "SKU-001",
        int quantity = 5,
        string? description = null)
        => ProductEntity.Create(name, price, sku, quantity, description);

    // ── Unit Tests ─────────────────────────────────────────────────────

    [Fact]
    public void Create_WithValidInputs_ReturnsProductWithCorrectProperties()
    {
        // Arrange & Act
        var product = ProductEntity.Create("Widget", 19.99m, "WDG-100", 10, "A fine widget");

        // Assert
        product.Name.Should().Be("Widget");
        product.Price.Should().Be(19.99m);
        product.Sku.Should().Be("WDG-100");
        product.Quantity.Should().Be(10);
        product.Description.Should().Be("A fine widget");
        product.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_WithNullDescription_SetsDescriptionToNull()
    {
        // Act
        var product = ProductEntity.Create("Widget", 9.99m, "WDG-100", 5);

        // Assert
        product.Description.Should().BeNull();
    }

    [Fact]
    public void Create_SetsIsActiveToTrue()
    {
        // Act
        var product = CreateValid();

        // Assert
        product.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_SetsCreatedAtToApproximatelyUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var product = CreateValid();

        // Assert
        var after = DateTime.UtcNow;
        product.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    // ── Validation: Name ───────────────────────────────────────────────

    [Fact]
    public void Create_WithEmptyName_ThrowsArgumentException()
    {
        var act = () => ProductEntity.Create(string.Empty, 9.99m, "SKU-001", 5);
        act.Should().Throw<ArgumentException>().And.ParamName.Should().Be("name");
    }

    [Fact]
    public void Create_WithWhitespaceName_ThrowsArgumentException()
    {
        var act = () => ProductEntity.Create("   ", 9.99m, "SKU-001", 5);
        act.Should().Throw<ArgumentException>().And.ParamName.Should().Be("name");
    }

    // ── Validation: Price ──────────────────────────────────────────────

    [Fact]
    public void Create_WithZeroPrice_ThrowsArgumentException()
    {
        var act = () => ProductEntity.Create("Widget", 0m, "SKU-001", 5);
        act.Should().Throw<ArgumentException>().And.ParamName.Should().Be("price");
    }

    [Fact]
    public void Create_WithNegativePrice_ThrowsArgumentException()
    {
        var act = () => ProductEntity.Create("Widget", -1m, "SKU-001", 5);
        act.Should().Throw<ArgumentException>().And.ParamName.Should().Be("price");
    }

    // ── Validation: SKU ────────────────────────────────────────────────

    [Fact]
    public void Create_WithEmptySku_ThrowsArgumentException()
    {
        var act = () => ProductEntity.Create("Widget", 9.99m, string.Empty, 5);
        act.Should().Throw<ArgumentException>().And.ParamName.Should().Be("sku");
    }

    [Fact]
    public void Create_WithWhitespaceSku_ThrowsArgumentException()
    {
        var act = () => ProductEntity.Create("Widget", 9.99m, "   ", 5);
        act.Should().Throw<ArgumentException>().And.ParamName.Should().Be("sku");
    }

    // ── Validation: Quantity ───────────────────────────────────────────

    [Fact]
    public void Create_WithZeroQuantity_ThrowsException()
    {
        var act = () => ProductEntity.Create("Widget", 9.99m, "SKU-001", 0);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithNegativeQuantity_ThrowsException()
    {
        var act = () => ProductEntity.Create("Widget", 9.99m, "SKU-001", -1);
        act.Should().Throw<ArgumentNullException>();
    }

    // ── HasBeenConfirmed ───────────────────────────────────────────────

    [Fact]
    public void HasBeenConfirmed_WithPositiveQuantity_DecrementsQuantityByOne()
    {
        // Arrange
        var product = CreateValid(quantity: 3);

        // Act
        product.HasBeenConfirmed();

        // Assert
        product.Quantity.Should().Be(2);
    }

    [Fact]
    public void HasBeenConfirmed_WithZeroQuantity_ThrowsInvalidOperationException()
    {
        // Arrange
        var product = CreateValid(quantity: 1);
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
    /// <remarks>Validates: Requirements 1.2, 1.6</remarks>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(DomainGenerators) })]
    public void Property7_CreatePreservesQuantity(PositiveInt positiveQuantity)
    {
        // Arrange
        var quantity = positiveQuantity.Get;

        // Act
        var product = ProductEntity.Create("Test Product", 9.99m, "SKU-001", quantity);

        // Assert
        product.Quantity.Should().Be(quantity);
    }

    /// <summary>
    /// Property 8: ProductEntity.Create rejects non-positive quantities.
    /// For any integer ≤ 0, Create throws an exception.
    /// </summary>
    /// <remarks>Validates: Requirements 1.6</remarks>
    [Property(MaxTest = 100)]
    public void Property8_CreateRejectsNonPositiveQuantities(int value)
    {
        // Arrange — constrain to non-positive values (≤ 0)
        var quantity = -Math.Abs(value);

        // Act
        var act = () => ProductEntity.Create("Test Product", 9.99m, "SKU-001", quantity);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Property 9: HasBeenConfirmed decrement invariance.
    /// For any product with initial quantity Q > 0 and N calls (1 ≤ N ≤ Q),
    /// Quantity equals Q − N.
    /// </summary>
    /// <remarks>Validates: Requirements 1.6</remarks>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(DomainGenerators) })]
    public void Property9_HasBeenConfirmedDecrementInvariance(PositiveInt positiveQuantity)
    {
        // Arrange
        var q = positiveQuantity.Get;
        var n = (q == 1) ? 1 : new Random().Next(1, q + 1);
        var product = ProductEntity.Create("Test Product", 9.99m, "SKU-001", q);

        // Act
        for (var i = 0; i < n; i++)
        {
            product.HasBeenConfirmed();
        }

        // Assert
        product.Quantity.Should().Be(q - n);
    }
}
