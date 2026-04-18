using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sample.TransactionalOutbox.Domain;

namespace Sample.TransactionalOutbox.Persistence.Configuration;

internal class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessageEntity>
{
    public void Configure(EntityTypeBuilder<InboxMessageEntity> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.MessageType).IsRequired();
        builder.Property(t => t.Payload).IsRequired();
        builder.Property(t => t.ProcessedAt).IsRequired(false);
        builder.Property(t => t.Error).IsRequired(false);
    }
}
