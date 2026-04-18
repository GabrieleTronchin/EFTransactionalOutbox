namespace Sample.TransactionalOutbox.Domain.Product;

public class ProductEntity
{
    private ProductEntity() { }

    public static ProductEntity Create(string name, decimal price, string sku, int quantity, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty or whitespace.", nameof(name));

        if (price <= 0)
            throw new ArgumentException("Price must be positive.", nameof(price));

        if (string.IsNullOrWhiteSpace(sku))
            throw new ArgumentException("SKU cannot be empty or whitespace.", nameof(sku));

        if (quantity <= 0)
            throw new ArgumentNullException(nameof(quantity));

        return new ProductEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            Price = price,
            Sku = sku,
            Quantity = quantity,
            Description = description,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public void HasBeenConfirmed()
    {
        if (Quantity == 0)
            throw new InvalidOperationException("product not available");
        Quantity--;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public decimal Price { get; private set; }
    public string Sku { get; private set; } = null!;
    public string? Description { get; private set; }
    public int Quantity { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
