using Loom.Domain.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loom.Infrastructure.Persistence.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> b)
    {
        b.ToTable("projects");
        b.HasKey(p => p.Id);

        b.Property(p => p.Slug)
            .HasConversion(ValueConverters.Slug)
            .HasMaxLength(80)
            .IsRequired();

        b.Property(p => p.Name).HasMaxLength(200).IsRequired();
        b.Property(p => p.Description).HasMaxLength(2000);
        b.Property(p => p.CreatedAt).IsRequired();
        b.Property(p => p.UpdatedAt).IsRequired();
        // DB-level default false matches the SQLite-bool-default pattern
        // (commit 26defda) and keeps the column NOT NULL on MSSQL too.
        b.Property(p => p.IsArchived).HasDefaultValue(false);

        b.HasIndex(p => p.Slug).IsUnique();
    }
}
