using Loom.Domain.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loom.Infrastructure.Persistence.Configurations;

internal sealed class AnnotationConfiguration : IEntityTypeConfiguration<Annotation>
{
    public void Configure(EntityTypeBuilder<Annotation> b)
    {
        b.ToTable("annotations");

        b.HasKey(a => a.Id);
        b.Property(a => a.Id).HasConversion(ValueConverters.AnnotationId);
        b.Property(a => a.ArtifactId).IsRequired();
        b.Property(a => a.UserId);
        b.Property(a => a.Kind).HasConversion<int>().IsRequired();
        b.Property(a => a.Body).IsRequired();
        b.Property(a => a.CreatedAt).IsRequired();

        b.OwnsOne(a => a.Target, t =>
        {
            t.Property(p => p.Kind).HasColumnName("target_kind").HasMaxLength(60);
            t.Property(p => p.Locator).HasColumnName("target_locator").HasMaxLength(2000);
        });

        b.Ignore(a => a.Tags);
        b.PrimitiveCollection<List<string>>("_tags")
            .HasColumnName("tags")
            .ElementType(c => c.HasMaxLength(60))
            .Metadata.SetPropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(a => a.ArtifactId);
        b.HasIndex(a => a.CreatedAt);
    }
}
