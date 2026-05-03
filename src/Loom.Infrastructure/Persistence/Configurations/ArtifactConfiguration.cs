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

        b.OwnsOne(a => a.Lock, x =>
        {
            x.Property(p => p.HolderUserId).HasColumnName("lock_holder_user_id");
            x.Property(p => p.AcquiredAt).HasColumnName("lock_acquired_at");
            x.Property(p => p.ExpiresAt).HasColumnName("lock_expires_at");
        });

        // Ignore the derived current-version property — it is computed from
        // the version collection, not persisted.
        b.Ignore(a => a.CurrentVersion);

        b.HasMany(a => a.Versions)
            .WithOne()
            .HasForeignKey(v => v.ArtifactId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Metadata.FindNavigation(nameof(Artifact.Versions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(a => a.NodeId);
        b.HasIndex(a => a.Kind);
    }
}

internal sealed class ArtifactVersionConfiguration : IEntityTypeConfiguration<ArtifactVersion>
{
    public void Configure(EntityTypeBuilder<ArtifactVersion> b)
    {
        b.ToTable("artifact_versions");

        b.HasKey(v => v.Id);
        b.Property(v => v.Id).HasConversion(ValueConverters.ArtifactVersionId);
        b.Property(v => v.ArtifactId).HasConversion(ValueConverters.ArtifactId);
        b.Property(v => v.VersionNumber).IsRequired();
        b.Property(v => v.Reason).HasMaxLength(500);
        b.Property(v => v.CreatedAt).IsRequired();

        b.OwnsOne(v => v.Author, x =>
        {
            x.Property(p => p.Kind).HasColumnName("author_kind").HasConversion<int>().IsRequired();
            x.Property(p => p.Id).HasColumnName("author_id").IsRequired();
        });

        b.OwnsOne(v => v.Content, x =>
        {
            x.Property(p => p.Uri).HasColumnName("content_uri").HasMaxLength(2000).IsRequired();
            x.Property(p => p.ContentType).HasColumnName("content_type").HasMaxLength(200).IsRequired();
            x.Property(p => p.SizeBytes).HasColumnName("content_size_bytes");
        });

        b.OwnsOne(v => v.Preview, x =>
        {
            x.Property(p => p.Uri).HasColumnName("preview_uri").HasMaxLength(2000);
            x.Property(p => p.ContentType).HasColumnName("preview_content_type").HasMaxLength(200);
            x.Property(p => p.SizeBytes).HasColumnName("preview_size_bytes");
        });

        b.HasIndex(v => new { v.ArtifactId, v.VersionNumber }).IsUnique();
    }
}
