using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sample.TransactionalOutbox.Domain.Order;

namespace Sample.TransactionalOutbox.Persistence.Configuration;

internal class OrderConfiguration : IEntityTypeConfiguration<OrderEntity>
{
    public void Configure(EntityTypeBuilder<OrderEntity> builder)
    {
        builder.HasKey(t => t.Id);
    }
}
