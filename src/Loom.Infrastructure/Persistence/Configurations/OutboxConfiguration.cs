using Loom.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loom.Infrastructure.Persistence.Configurations;

internal sealed class OutboxConfiguration : IEntityTypeConfiguration<OutboxEntry>
{
    public void Configure(EntityTypeBuilder<OutboxEntry> b)
    {
        b.ToTable("outbox");

        b.HasKey(e => e.Id);
        b.Property(e => e.Id).IsRequired();
        b.Property(e => e.Sequence).ValueGeneratedOnAdd().UseIdentityColumn();
        b.Property(e => e.OccurredAt).IsRequired();
        b.Property(e => e.EventType).HasMaxLength(200).IsRequired();
        b.Property(e => e.PayloadJson).IsRequired();
        b.Property(e => e.ProcessedAt);
        b.Property(e => e.Attempts).IsRequired();

        b.HasIndex(e => e.Sequence).IsUnique();
        b.HasIndex(e => new { e.ProcessedAt, e.OccurredAt });
    }
}
