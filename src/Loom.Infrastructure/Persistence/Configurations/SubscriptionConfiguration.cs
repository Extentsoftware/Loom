using Loom.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loom.Infrastructure.Persistence.Configurations;

internal sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> b)
    {
        b.ToTable("subscriptions");

        b.HasKey(s => s.Id);
        b.Property(s => s.Id).HasConversion(ValueConverters.SubscriptionId);
        b.Property(s => s.UserId).IsRequired();
        b.Property(s => s.NodeId).HasConversion(ValueConverters.NodeId);
        b.Property(s => s.EventType).HasConversion<int>().IsRequired();
        b.Property(s => s.Channel).HasConversion<int>().IsRequired();
        b.Property(s => s.Mode).HasConversion<int>().IsRequired();
        b.Property(s => s.CreatedAt).IsRequired();
        b.Property(s => s.UpdatedAt).IsRequired();

        // One row per (user, node, event-type, channel) — multiple rows
        // for the same user+node mean cross-channel delivery.
        b.HasIndex(s => new { s.UserId, s.NodeId, s.EventType, s.Channel }).IsUnique();
        b.HasIndex(s => new { s.NodeId, s.EventType });
    }
}
