using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sample.TransactionalOutbox.Domain;

namespace Sample.TransactionalOutbox.Persistence;

internal class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessageEntity>
{
    public void Configure(EntityTypeBuilder<OutboxMessageEntity> builder)
    {
        builder.HasKey(t => t.Id);
    }
}
