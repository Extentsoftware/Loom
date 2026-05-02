using Loom.Domain.Fragments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loom.Infrastructure.Persistence.Configurations;

internal sealed class FragmentConfiguration : IEntityTypeConfiguration<Fragment>
{
    public void Configure(EntityTypeBuilder<Fragment> b)
    {
        b.ToTable("fragments");

        b.HasKey(f => f.Id);
        b.Property(f => f.Id).HasConversion(ValueConverters.FragmentId);

        b.Property(f => f.Key)
            .HasConversion(ValueConverters.Slug)
            .HasMaxLength(80)
            .IsRequired();

        b.Property(f => f.Category).HasConversion<int>().IsRequired();
        b.Property(f => f.Scope).HasConversion<int>().IsRequired();
        b.Property(f => f.ScopeId);
        b.Property(f => f.Title).HasMaxLength(300).IsRequired();
        b.Property(f => f.OwnerId).IsRequired();
        b.Property(f => f.CreatedAt).IsRequired();
        b.Property(f => f.UpdatedAt).IsRequired();
        b.Property(f => f.CurrentVersionId).HasConversion(ValueConverters.NullableFragmentVersionId);

        // (key, scope, scopeId) is unique — same key can exist as both global
        // and project-scoped, but never twice within the same scope.
        b.HasIndex(f => new { f.Key, f.Scope, f.ScopeId }).IsUnique();
        b.HasIndex(f => f.Category);

        b.PrimitiveCollection<List<string>>("_tags")
            .HasColumnName("tags")
            .ElementType(c => c.HasMaxLength(50));
        b.Navigation("_tags").UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasMany<FragmentVersion>("_versions")
            .WithOne()
            .HasForeignKey(v => v.FragmentId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Navigation("_versions").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class FragmentVersionConfiguration : IEntityTypeConfiguration<FragmentVersion>
{
    public void Configure(EntityTypeBuilder<FragmentVersion> b)
    {
        b.ToTable("fragment_versions");

        b.HasKey(v => v.Id);
        b.Property(v => v.Id).HasConversion(ValueConverters.FragmentVersionId);
        b.Property(v => v.FragmentId).HasConversion(ValueConverters.FragmentId);
        b.Property(v => v.Version).IsRequired();
        b.Property(v => v.Content).IsRequired();
        b.Property(v => v.ChangeNote).HasMaxLength(1000);
        b.Property(v => v.AuthorId).IsRequired();
        b.Property(v => v.CreatedAt).IsRequired();
        b.Property(v => v.IsDeprecated).IsRequired();

        // EngineHints persisted as a complex owned type.
        b.OwnsOne(v => v.Hints, h =>
        {
            h.Property(x => x.PrefersExtendedThinking).HasColumnName("hints_prefers_extended_thinking");
            h.Property(x => x.MaxContextTokens).HasColumnName("hints_max_context_tokens");
            h.Property(x => x.RequiresJsonOutput).HasColumnName("hints_requires_json_output");
            h.Property(x => x.RequiresFilesystem).HasColumnName("hints_requires_filesystem");
            h.Property(x => x.PreferredModelHint).HasColumnName("hints_preferred_model").HasMaxLength(100);
        });

        b.HasIndex(v => new { v.FragmentId, v.Version }).IsUnique();
    }
}
