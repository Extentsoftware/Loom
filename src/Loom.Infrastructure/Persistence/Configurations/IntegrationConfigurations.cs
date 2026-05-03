using Loom.Domain.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loom.Infrastructure.Persistence.Configurations;

internal sealed class IntegrationConnectionConfiguration : IEntityTypeConfiguration<IntegrationConnection>
{
    public void Configure(EntityTypeBuilder<IntegrationConnection> b)
    {
        b.ToTable("integration_connections");

        b.HasKey(c => c.Id);
        b.Property(c => c.Id).HasConversion(ValueConverters.IntegrationConnectionId);
        b.Property(c => c.ProjectId);
        b.Property(c => c.Kind).HasConversion<int>().IsRequired();
        b.Property(c => c.DisplayName).HasMaxLength(200).IsRequired();
        b.Property(c => c.TenantOrAccount).HasMaxLength(500).IsRequired();
        b.Property(c => c.SecretRef).HasMaxLength(1000);
        b.Property(c => c.WebhookSecret).HasMaxLength(500);
        b.Property(c => c.Status).HasConversion<int>().IsRequired();
        b.Property(c => c.CreatedAt).IsRequired();
        b.Property(c => c.UpdatedAt).IsRequired();
        b.Property(c => c.LastHealthCheckAt);

        b.HasIndex(c => new { c.Kind, c.ProjectId });
    }
}

internal sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> b)
    {
        b.ToTable("webhook_deliveries");

        b.HasKey(w => w.Id);
        b.Property(w => w.Id).HasConversion(ValueConverters.WebhookDeliveryId);
        b.Property(w => w.ConnectionId).HasConversion(ValueConverters.NullableIntegrationConnectionId);
        b.Property(w => w.Kind).HasConversion<int>().IsRequired();
        b.Property(w => w.EventType).HasMaxLength(200).IsRequired();
        b.Property(w => w.HeadersJson).IsRequired();
        b.Property(w => w.BodyJson).IsRequired();
        b.Property(w => w.Status).HasConversion<int>().IsRequired();
        b.Property(w => w.FailureReason).HasMaxLength(2000);
        b.Property(w => w.ReceivedAt).IsRequired();
        b.Property(w => w.ProcessedAt);

        b.HasIndex(w => new { w.ConnectionId, w.ReceivedAt });
        b.HasIndex(w => w.ReceivedAt);
    }
}
