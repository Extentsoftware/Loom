using Loom.Domain.Runs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loom.Infrastructure.Persistence.Configurations;

internal sealed class RunConfiguration : IEntityTypeConfiguration<Run>
{
    public void Configure(EntityTypeBuilder<Run> b)
    {
        b.ToTable("runs");

        b.HasKey(r => r.Id);
        b.Property(r => r.Id).HasConversion(ValueConverters.RunId);
        b.Property(r => r.NodeId).HasConversion(ValueConverters.NodeId);

        b.Property(r => r.WorkflowId);
        b.Property(r => r.StepId);
        b.Property(r => r.Engine).HasConversion<int>().IsRequired();
        b.Property(r => r.ExternalRunId).HasMaxLength(200);
        b.Property(r => r.State).HasConversion<int>().IsRequired();
        b.Property(r => r.FailureReason).HasMaxLength(2000);
        b.Property(r => r.CreatedAt).IsRequired();
        b.Property(r => r.StartedAt);
        b.Property(r => r.CompletedAt);
        b.Property(r => r.AssembledPromptId).HasConversion(ValueConverters.NullableAssembledPromptId);

        // Budgets and Cost are inline value objects, not entities. EF
        // Core 8+ provides `ComplexProperty` for exactly this use case:
        // same column layout as OwnsOne, but no synthetic FK, no
        // change-tracker entity entry, and no "is this dependent being
        // moved to a new principal?" check. The previous OwnsOne mapping
        // tripped that check on every second SaveChanges within one DI
        // scope ("Run.Budgets#Budgets.RunId is part of a key and so
        // cannot be modified") — that whole class of error goes away
        // with complex properties.
        b.ComplexProperty(r => r.Budgets, x =>
        {
            x.IsRequired();
            x.Property(p => p.MaxInputTokens).HasColumnName("budgets_max_input_tokens");
            x.Property(p => p.MaxOutputTokens).HasColumnName("budgets_max_output_tokens");
            x.Property(p => p.MaxWallClock).HasColumnName("budgets_max_wall_clock");
            x.Property(p => p.MaxCostUsd).HasColumnName("budgets_max_cost_usd").HasPrecision(10, 4);
        });

        // Cost is optional (null at Queue, set on Complete/Fail).
        b.ComplexProperty(r => r.Cost, x =>
        {
            x.IsRequired(false);
            x.Property(p => p.Model).HasColumnName("cost_model").HasMaxLength(100);
            x.Property(p => p.Deployment).HasColumnName("cost_deployment").HasMaxLength(200);
            x.Property(p => p.InputTokens).HasColumnName("cost_input_tokens");
            x.Property(p => p.OutputTokens).HasColumnName("cost_output_tokens");
            x.Property(p => p.UsdAmount).HasColumnName("cost_usd_amount").HasPrecision(10, 4);
        });

        b.Ignore(r => r.Fragments);

        b.OwnsMany<FragmentRef>("_fragments", o =>
        {
            o.ToTable("run_fragments");
            o.WithOwner().HasForeignKey("run_id");
            o.Property<int>("ordinal");
            o.HasKey("run_id", "ordinal");
            o.Property(x => x.FragmentId).HasConversion(ValueConverters.FragmentId);
            o.Property(x => x.VersionId).HasConversion(ValueConverters.FragmentVersionId);
            o.Property(x => x.Version).IsRequired();
        }).Navigation("_fragments").UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(r => r.NodeId);
        b.HasIndex(r => r.State);
        b.HasIndex(r => r.CreatedAt);
    }
}
