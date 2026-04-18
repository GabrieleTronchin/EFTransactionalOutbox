using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sample.TransactionalOutbox.Domain.Product;

namespace Sample.TransactionalOutbox.Persistence.Configuration;

internal class ProductConfiguration : IEntityTypeConfiguration<ProductEntity>
{
    public void Configure(EntityTypeBuilder<ProductEntity> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).IsRequired();
        builder.Property(t => t.Price).IsRequired();
        builder.Property(t => t.Sku).IsRequired();
        builder.Property(t => t.Description).IsRequired(false);
    }
}
