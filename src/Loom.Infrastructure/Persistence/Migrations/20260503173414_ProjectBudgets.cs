using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loom.Infrastructure.Persistence.Migrations;

/// <summary>
/// Phase 5 migration: per-project rolling budget with daily $-cap circuit
/// breaker. Strictly additive on top of 20260503171255. One row per
/// project (unique index on ProjectId); rows are created lazily by the
/// budget service when a project first runs an agent.
/// </summary>
public partial class ProjectBudgets : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "project_budgets",
            schema: "loom",
            columns: table => new
            {
                Id = table.Column<System.Guid>(type: "uniqueidentifier", nullable: false),
                ProjectId = table.Column<System.Guid>(type: "uniqueidentifier", nullable: false),
                DailyCapUsd = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                TodaysSpendUsd = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                DayAnchor = table.Column<System.DateOnly>(type: "date", nullable: false),
                CreatedAt = table.Column<System.DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<System.DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_project_budgets", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_project_budgets_ProjectId",
            schema: "loom",
            table: "project_budgets",
            column: "ProjectId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "project_budgets",
            schema: "loom");
    }
}
