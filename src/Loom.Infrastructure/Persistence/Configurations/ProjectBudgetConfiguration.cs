using Loom.Domain.BudgetControl;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loom.Infrastructure.Persistence.Configurations;

internal sealed class ProjectBudgetConfiguration : IEntityTypeConfiguration<ProjectBudget>
{
    public void Configure(EntityTypeBuilder<ProjectBudget> b)
    {
        b.ToTable("project_budgets");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(ValueConverters.ProjectBudgetId);
        b.Property(x => x.ProjectId).IsRequired();
        b.Property(x => x.DailyCapUsd).HasColumnType("decimal(18,4)");
        b.Property(x => x.TodaysSpendUsd).HasColumnType("decimal(18,4)").IsRequired();
        b.Property(x => x.DayAnchor).IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt).IsRequired();

        b.HasIndex(x => x.ProjectId).IsUnique();
    }
}
