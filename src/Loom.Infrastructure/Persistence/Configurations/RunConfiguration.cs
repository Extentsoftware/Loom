using Loom.Domain.Fragments;
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

        b.OwnsOne(r => r.Budgets, x =>
        {
            x.Property(p => p.MaxInputTokens).HasColumnName("budgets_max_input_tokens");
            x.Property(p => p.MaxOutputTokens).HasColumnName("budgets_max_output_tokens");
            x.Property(p => p.MaxWallClock).HasColumnName("budgets_max_wall_clock");
            x.Property(p => p.MaxCostUsd).HasColumnName("budgets_max_cost_usd").HasPrecision(10, 4);
        });

        b.OwnsOne(r => r.Cost, x =>
        {
            x.Property(p => p.Model).HasColumnName("cost_model").HasMaxLength(100);
            x.Property(p => p.Deployment).HasColumnName("cost_deployment").HasMaxLength(200);
            x.Property(p => p.InputTokens).HasColumnName("cost_input_tokens");
            x.Property(p => p.OutputTokens).HasColumnName("cost_output_tokens");
            x.Property(p => p.UsdAmount).HasColumnName("cost_usd_amount").HasPrecision(10, 4);
        });

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
