using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loom.Infrastructure.Persistence.Migrations;

/// <summary>
/// Catches the model snapshot up to two changes that landed without
/// migrations: (1) Run.Budgets navigation marked IsRequired (metadata
/// only — no SQL body needed), and (2) DB-level <c>false</c> defaults
/// on the FragmentVersion engine-hint bool columns (introduced for
/// SQLite parity in commit 26defda; the MSSQL ALTERs catch up here).
/// </summary>
public partial class RunBudgetsRequiredNavigation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<bool>(
            name: "hints_requires_json_output",
            schema: "loom",
            table: "fragment_versions",
            type: "bit",
            nullable: false,
            defaultValue: false,
            oldClrType: typeof(bool),
            oldType: "bit");

        migrationBuilder.AlterColumn<bool>(
            name: "hints_requires_filesystem",
            schema: "loom",
            table: "fragment_versions",
            type: "bit",
            nullable: false,
            defaultValue: false,
            oldClrType: typeof(bool),
            oldType: "bit");

        migrationBuilder.AlterColumn<bool>(
            name: "hints_prefers_extended_thinking",
            schema: "loom",
            table: "fragment_versions",
            type: "bit",
            nullable: false,
            defaultValue: false,
            oldClrType: typeof(bool),
            oldType: "bit");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<bool>(
            name: "hints_requires_json_output",
            schema: "loom",
            table: "fragment_versions",
            type: "bit",
            nullable: false,
            oldClrType: typeof(bool),
            oldType: "bit",
            oldDefaultValue: false);

        migrationBuilder.AlterColumn<bool>(
            name: "hints_requires_filesystem",
            schema: "loom",
            table: "fragment_versions",
            type: "bit",
            nullable: false,
            oldClrType: typeof(bool),
            oldType: "bit",
            oldDefaultValue: false);

        migrationBuilder.AlterColumn<bool>(
            name: "hints_prefers_extended_thinking",
            schema: "loom",
            table: "fragment_versions",
            type: "bit",
            nullable: false,
            oldClrType: typeof(bool),
            oldType: "bit",
            oldDefaultValue: false);
    }
}
