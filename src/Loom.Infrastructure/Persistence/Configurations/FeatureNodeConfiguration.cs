using Loom.Domain.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loom.Infrastructure.Persistence.Configurations;

internal sealed class FeatureNodeConfiguration : IEntityTypeConfiguration<FeatureNode>
{
    public void Configure(EntityTypeBuilder<FeatureNode> b)
    {
        b.ToTable("nodes");

        b.HasKey(n => n.Id);
        b.Property(n => n.Id).HasConversion(ValueConverters.NodeId);

        b.Property(n => n.ParentId).HasConversion(ValueConverters.NullableNodeId);
        b.Property(n => n.ProjectId).IsRequired();

        b.Property(n => n.Slug)
            .HasConversion(ValueConverters.Slug)
            .HasMaxLength(80)
            .IsRequired();

        b.Property(n => n.Type).HasConversion<int>().IsRequired();
        b.Property(n => n.Phase).HasConversion<int>().IsRequired();
        b.Property(n => n.Title).HasMaxLength(300).IsRequired();
        b.Property(n => n.Intent).HasMaxLength(2000);
        b.Property(n => n.OwnerId).IsRequired();
        b.Property(n => n.CreatedAt).IsRequired();
        b.Property(n => n.UpdatedAt).IsRequired();

        // Optimistic concurrency: Version is a uint that the domain
        // increments on every state change (see FeatureNode.cs). EF
        // treats it as a non-server-generated concurrency token —
        // matches both MSSQL and SQLite without provider-specific
        // workarounds. (The previous IsRowVersion() configuration
        // mismatched the uint property type with the rowversion byte[]
        // column, which fell over on first INSERT round-trip.)
        b.Property(n => n.Version)
            .IsConcurrencyToken()
            .ValueGeneratedNever()
            .HasDefaultValue(0u);

        // Self-referencing tree.
        b.HasOne<FeatureNode>()
            .WithMany()
            .HasForeignKey(n => n.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Slug must be unique within a project; nodes from different projects may share a slug.
        b.HasIndex(n => new { n.ProjectId, n.Slug }).IsUnique();
        b.HasIndex(n => n.ParentId);
        b.HasIndex(n => n.ProjectId);

        // The public read-only adapters (Outcomes, Hypotheses, Constraints,
        // Stakeholders, OpenQuestions) wrap private backing fields — EF is
        // told to ignore them so the OwnsMany / PrimitiveCollection bindings
        // below take effect against the fields directly.
        b.Ignore(n => n.Outcomes);
        b.Ignore(n => n.Hypotheses);
        b.Ignore(n => n.Constraints);
        b.Ignore(n => n.Stakeholders);
        b.Ignore(n => n.OpenQuestions);

        // Owned collections — outcomes, hypotheses, constraints, open questions, stakeholders.
        // Stored as side tables, not JSON, so they remain queryable and reportable.
        b.OwnsMany<Outcome>("_outcomes", o =>
        {
            o.ToTable("node_outcomes");
            o.WithOwner().HasForeignKey("node_id");
            o.Property<int>("ordinal");
            o.HasKey("node_id", "ordinal");
            o.Property(x => x.Statement).HasMaxLength(1000).IsRequired();
            o.Property(x => x.MetricHint).HasMaxLength(500);
            o.Property(x => x.Measurable).IsRequired();
        }).Navigation("_outcomes").UsePropertyAccessMode(PropertyAccessMode.Field);

        b.OwnsMany<Hypothesis>("_hypotheses", o =>
        {
            o.ToTable("node_hypotheses");
            o.WithOwner().HasForeignKey("node_id");
            o.Property<int>("ordinal");
            o.HasKey("node_id", "ordinal");
            o.Property(x => x.If).HasMaxLength(500).IsRequired();
            o.Property(x => x.Then).HasMaxLength(500).IsRequired();
            o.Property(x => x.Because).HasMaxLength(1000).IsRequired();
        }).Navigation("_hypotheses").UsePropertyAccessMode(PropertyAccessMode.Field);

        b.OwnsMany<Constraint>("_constraints", o =>
        {
            o.ToTable("node_constraints");
            o.WithOwner().HasForeignKey("node_id");
            o.Property<int>("ordinal");
            o.HasKey("node_id", "ordinal");
            o.Property(x => x.Kind).HasConversion<int>().IsRequired();
            o.Property(x => x.Detail).HasMaxLength(1000).IsRequired();
        }).Navigation("_constraints").UsePropertyAccessMode(PropertyAccessMode.Field);

        b.OwnsMany<Stakeholder>("_stakeholders", o =>
        {
            o.ToTable("node_stakeholders");
            o.WithOwner().HasForeignKey("node_id");
            o.Property<int>("ordinal");
            o.HasKey("node_id", "ordinal");
            o.Property(x => x.UserId);
            o.Property(x => x.Name).HasMaxLength(200).IsRequired();
            o.Property(x => x.Role).HasMaxLength(100).IsRequired();
            o.Property(x => x.Interest).HasMaxLength(500);
        }).Navigation("_stakeholders").UsePropertyAccessMode(PropertyAccessMode.Field);

        b.PrimitiveCollection<List<string>>("_openQuestions")
            .HasColumnName("open_questions")
            .ElementType(c => c.HasMaxLength(500))
            .Metadata.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
