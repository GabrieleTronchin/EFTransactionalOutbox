using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sample.TransactionalOutbox.Domain.Product;

namespace Sample.TransactionalOutbox.Persistence.Configuration;

internal class ProductConfiguration : IEntityTypeConfiguration<ProductEntity>
{
    public void Configure(EntityTypeBuilder<ProductEntity> builder)
    {
        builder.HasKey(t => t.Id);
    }
}

