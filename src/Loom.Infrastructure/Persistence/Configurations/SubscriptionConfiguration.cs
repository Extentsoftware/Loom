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
        // Either NodeId or ProjectId is populated, not both. The aggregate
        // enforces this; the column is nullable here to match the model.
        b.Property(s => s.NodeId).HasConversion(ValueConverters.NullableNodeId);
        b.Property(s => s.ProjectId);
        b.Property(s => s.EventType).HasConversion<int>().IsRequired();
        b.Property(s => s.Channel).HasConversion<int>().IsRequired();
        b.Property(s => s.Mode).HasConversion<int>().IsRequired();
        b.Property(s => s.Role).HasConversion<int?>();
        b.Property(s => s.CreatedAt).IsRequired();
        b.Property(s => s.UpdatedAt).IsRequired();

        // Compound-unique on the full filter shape; multiple rows for the
        // same (user, scope, event, channel) only legal when role differs.
        b.HasIndex(s => new { s.UserId, s.NodeId, s.ProjectId, s.EventType, s.Channel, s.Role }).IsUnique();
        // Lookup index for the dispatcher's per-event match query.
        b.HasIndex(s => new { s.NodeId, s.EventType });
        b.HasIndex(s => new { s.ProjectId, s.EventType });
    }
}
