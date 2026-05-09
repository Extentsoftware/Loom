using Loom.Domain.Artifacts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loom.Infrastructure.Persistence.Configurations;

internal sealed class ProjectArtifactConfiguration : IEntityTypeConfiguration<ProjectArtifact>
{
    public void Configure(EntityTypeBuilder<ProjectArtifact> b)
    {
        b.ToTable("project_artifacts");

        b.HasKey(a => a.Id);
        b.Property(a => a.Id).HasConversion(ValueConverters.ProjectArtifactId);
        b.Property(a => a.ProjectId).IsRequired();
        b.Property(a => a.NodeId).HasConversion(ValueConverters.NullableNodeId);
        b.Property(a => a.Kind).HasConversion<int>().IsRequired();
        b.Property(a => a.Payload).HasConversion<int>().IsRequired();
        b.Property(a => a.Label).HasMaxLength(300).IsRequired();
        b.Property(a => a.Description).HasMaxLength(2000);
        b.Property(a => a.Url).HasMaxLength(2000);
        b.Property(a => a.CreatedByUserId);
        b.Property(a => a.CreatedAt).IsRequired();

        // Blob handle. Null for link-typed artifacts; populated for file-typed.
        b.OwnsOne(a => a.Blob, x =>
        {
            x.Property(p => p.Uri).HasColumnName("blob_uri").HasMaxLength(2000);
            x.Property(p => p.ContentType).HasColumnName("blob_content_type").HasMaxLength(200);
            x.Property(p => p.SizeBytes).HasColumnName("blob_size_bytes");
        });

        b.HasIndex(a => a.ProjectId);
        b.HasIndex(a => new { a.ProjectId, a.NodeId });
    }
}

internal sealed class ArtifactBlobConfiguration : IEntityTypeConfiguration<ArtifactBlob>
{
    public void Configure(EntityTypeBuilder<ArtifactBlob> b)
    {
        b.ToTable("artifact_blobs");

        b.HasKey(x => x.Id);
        b.Property(x => x.Payload).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(200).IsRequired();
        b.Property(x => x.SizeBytes).IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();
    }
}
