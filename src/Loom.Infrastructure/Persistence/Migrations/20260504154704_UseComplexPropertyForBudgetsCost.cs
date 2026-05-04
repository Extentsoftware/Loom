using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loom.Infrastructure.Persistence.Migrations;

/// <summary>
/// Snapshot-sync migration. RunConfiguration switched <c>Budgets</c> and
/// <c>Cost</c> from <c>OwnsOne</c> (owned-entity-inline) to
/// <c>ComplexProperty</c> (inline value-object) per EF Core 8+ guidance.
/// Storage layout is identical (same column names, same types), so the
/// SQL body is empty; only the model snapshot needs reconciling.
///
/// The fix removes EF's owned-type FK tracking on the Budgets/Cost
/// instances, which was triggering "Run.Budgets#Budgets.RunId is part
/// of a key and so cannot be modified" on every second SaveChanges
/// within a tracked DI scope.
/// </summary>
public partial class UseComplexPropertyForBudgetsCost : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
