using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loom.Infrastructure.Persistence.Migrations;

/// <summary>
/// Widens <c>artifacts.canonical_external_id</c> from <c>nvarchar(500)</c>
/// to <c>nvarchar(max)</c> so hub-native artifacts (acceptance criteria,
/// risks, wireframe payloads) can carry their full JSON body inline.
/// External-store artifacts still write short identifiers and pay no cost
/// for the wider column.
/// </summary>
public partial class WidenArtifactExternalId : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "canonical_external_id",
            schema: "loom",
            table: "artifacts",
            type: "nvarchar(max)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(500)",
            oldMaxLength: 500);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "canonical_external_id",
            schema: "loom",
            table: "artifacts",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)");
    }
}
