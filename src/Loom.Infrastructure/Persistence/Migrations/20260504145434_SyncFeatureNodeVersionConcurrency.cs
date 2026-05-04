using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loom.Infrastructure.Persistence.Migrations;

/// <summary>
/// Snapshot-sync migration. The previous configuration mapped
/// FeatureNode.Version with <c>IsRowVersion()</c>, which mismatched the
/// uint property type and broke INSERT round-trips on MSSQL. The
/// configuration now uses <c>IsConcurrencyToken().ValueGeneratedNever()</c>.
/// The Initial migration was patched (per its README note) to declare the
/// column as <c>bigint NOT NULL DEFAULT 0</c> instead of <c>rowversion</c>;
/// this migration reconciles the model snapshot so subsequent
/// <c>dotnet ef migrations add</c> calls produce clean diffs. The SQL is
/// effectively a no-op on a fresh database.
/// </summary>
public partial class SyncFeatureNodeVersionConcurrency : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<long>(
            name: "Version",
            schema: "loom",
            table: "nodes",
            type: "bigint",
            nullable: false,
            defaultValue: 0L,
            oldClrType: typeof(long),
            oldType: "bigint",
            oldRowVersion: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<long>(
            name: "Version",
            schema: "loom",
            table: "nodes",
            type: "bigint",
            rowVersion: true,
            nullable: false,
            oldClrType: typeof(long),
            oldType: "bigint",
            oldDefaultValue: 0L);
    }
}
