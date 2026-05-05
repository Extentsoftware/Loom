using Loom.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loom.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notifications");

        b.HasKey(n => n.Id);
        b.Property(n => n.Id).HasConversion(ValueConverters.NotificationId);
        b.Property(n => n.UserId).IsRequired();
        b.Property(n => n.NodeId).HasConversion(ValueConverters.NullableNodeId);
        b.Property(n => n.RunId).HasConversion(ValueConverters.NullableRunId);
        b.Property(n => n.EventType).HasConversion<int>().IsRequired();
        b.Property(n => n.Title).HasMaxLength(200).IsRequired();
        b.Property(n => n.Body).HasMaxLength(4000).IsRequired();
        b.Property(n => n.DeepLink).HasMaxLength(500);
        b.Property(n => n.Severity).HasConversion<int>().IsRequired();
        b.Property(n => n.CreatedAt).IsRequired();
        b.Property(n => n.ReadAt);

        // Bell-icon feed query: unread per user, newest first.
        b.HasIndex(n => new { n.UserId, n.ReadAt, n.CreatedAt });
    }
}
