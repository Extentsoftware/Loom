using System.Text.Json;
using Loom.Domain.Common;
using Loom.Domain.Fragments;
using Loom.Domain.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Loom.Infrastructure.Persistence.Configurations;

internal sealed class WorkflowConfiguration : IEntityTypeConfiguration<Workflow>
{
    public void Configure(EntityTypeBuilder<Workflow> b)
    {
        b.ToTable("workflows");

        b.HasKey(w => w.Id);
        b.Property(w => w.Id).HasConversion(ValueConverters.WorkflowId);
        b.Property(w => w.Key)
            .HasConversion(ValueConverters.Slug)
            .HasMaxLength(80)
            .IsRequired();
        b.Property(w => w.Version).IsRequired();
        b.Property(w => w.Title).HasMaxLength(300).IsRequired();
        b.Property(w => w.CreatedAt).IsRequired();

        // (Key, Version) is unique — workflows are versioned per key.
        b.HasIndex(w => new { w.Key, w.Version }).IsUnique();

        b.Ignore(w => w.Steps);

        b.OwnsMany<WorkflowStep>("_steps", o =>
        {
            o.ToTable("workflow_steps");
            o.WithOwner().HasForeignKey(s => s.WorkflowId);
            o.HasKey(s => s.Id);
            o.Property(s => s.Id).HasConversion(ValueConverters.WorkflowStepId);
            o.Property(s => s.WorkflowId).HasConversion(ValueConverters.WorkflowId);
            o.Property(s => s.Order).IsRequired();
            o.Property(s => s.Key).HasMaxLength(80).IsRequired();
            o.Property(s => s.Kind).HasConversion<int>().IsRequired();
            o.Property(s => s.Gating).HasConversion<int>().IsRequired();
            o.Property(s => s.EnginePref).HasConversion<int?>();
            o.Property(s => s.OutputSchemaName).HasMaxLength(200);

            o.OwnsOne(s => s.Budgets, x =>
            {
                x.Property(p => p.MaxInputTokens).HasColumnName("budgets_max_input_tokens");
                x.Property(p => p.MaxOutputTokens).HasColumnName("budgets_max_output_tokens");
                x.Property(p => p.MaxWallClock).HasColumnName("budgets_max_wall_clock");
                x.Property(p => p.MaxCostUsd).HasColumnName("budgets_max_cost_usd").HasPrecision(10, 4);
            });

            // FragmentSelectors persisted as a single JSON column. Selectors
            // are small, immutable per workflow version, and never queried by
            // contents — JSON is the right shape. Backed by the private
            // _fragmentSelectors list on WorkflowStep.
            var sel = o.Property<List<FragmentSelector>>("_fragmentSelectors")
                .HasColumnName("selectors")
                .HasConversion(SelectorJsonConverter, SelectorListComparer);
            sel.Metadata.SetPropertyAccessMode(PropertyAccessMode.Field);

            o.Ignore(s => s.FragmentSelectors);
            o.HasIndex(s => new { s.WorkflowId, s.Order });
        });

        b.Navigation("_steps").UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    /// <summary>
    /// Wire shape for FragmentSelector inside the JSON column. Storing the
    /// selectors via a converter rather than via the domain record directly
    /// gives us insulation if FragmentSelector ever grows fields the schema
    /// shouldn't see.
    /// </summary>
    private sealed record SelectorRow(int Category, string Key);

    private static readonly ValueConverter<List<FragmentSelector>, string> SelectorJsonConverter =
        new(
            v => JsonSerializer.Serialize(
                (v ?? new List<FragmentSelector>())
                    .Select(s => new SelectorRow((int)s.Category, s.Key.Value))
                    .ToList(),
                (JsonSerializerOptions?)null),
            v => string.IsNullOrWhiteSpace(v)
                ? new List<FragmentSelector>()
                : (JsonSerializer.Deserialize<List<SelectorRow>>(v, (JsonSerializerOptions?)null) ?? new List<SelectorRow>())
                    .Select(r => new FragmentSelector((FragmentCategory)r.Category, Slug.From(r.Key)))
                    .ToList());

    private static readonly ValueComparer<List<FragmentSelector>> SelectorListComparer =
        new(
            (a, b) => (a ?? new List<FragmentSelector>()).SequenceEqual(b ?? new List<FragmentSelector>()),
            v => v.Aggregate(0, (h, x) => HashCode.Combine(h, x.GetHashCode())),
            v => v.ToList());
}
