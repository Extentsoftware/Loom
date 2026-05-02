using Loom.Domain.Artifacts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loom.Infrastructure.Persistence.Configurations;

internal sealed class ArtifactConfiguration : IEntityTypeConfiguration<Artifact>
{
    public void Configure(EntityTypeBuilder<Artifact> b)
    {
        b.ToTable("artifacts");

        b.HasKey(a => a.Id);
        b.Property(a => a.Id).HasConversion(ValueConverters.ArtifactId);
        b.Property(a => a.NodeId).HasConversion(ValueConverters.NodeId);
        b.Property(a => a.Kind).HasConversion<int>().IsRequired();
        b.Property(a => a.Title).HasMaxLength(300).IsRequired();
        b.Property(a => a.CreatedAt).IsRequired();
        b.Property(a => a.UpdatedAt).IsRequired();
        b.Property(a => a.LastSyncedAt);

        b.OwnsOne(a => a.Canonical, x =>
        {
            x.Property(p => p.Store).HasColumnName("canonical_store").HasConversion<int>().IsRequired();
            x.Property(p => p.ExternalId).HasColumnName("canonical_external_id").HasMaxLength(500).IsRequired();
            x.Property(p => p.Url).HasColumnName("canonical_url").HasMaxLength(2000);
        });

        b.HasIndex(a => a.NodeId);
        b.HasIndex(a => a.Kind);
    }
}
