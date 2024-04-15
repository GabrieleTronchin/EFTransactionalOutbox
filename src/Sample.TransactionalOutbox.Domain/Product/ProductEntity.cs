namespace Sample.TransactionalOutbox.Domain.Product;
public class ProductEntity
{
    private ProductEntity()
    {
    }

    public static ProductEntity Create(int quantity)
    {

        if (quantity <= 0) throw new ArgumentNullException(nameof(quantity));

        return new ProductEntity
        {
            Id = Guid.NewGuid(),
            Quantity = quantity,
        };
    }

    public void HasBeenConfirmed()
    {
        if (Quantity == 0) throw new InvalidOperationException("product not available");
        Quantity--;
    }

    public Guid Id { get; private set; }
    public int Quantity { get; private set; }

}
