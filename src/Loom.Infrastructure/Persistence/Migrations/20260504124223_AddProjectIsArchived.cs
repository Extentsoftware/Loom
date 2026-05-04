using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loom.Infrastructure.Persistence.Migrations;

/// <summary>
/// Adds an <c>IsArchived</c> bit column to <c>projects</c> with a DB-level
/// default of <c>false</c>. Operating Picture filters archived projects
/// by default; the "Show archived" toggle uses
/// <c>IProjectRepository.ListAllAsync</c>.
/// </summary>
public partial class AddProjectIsArchived : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsArchived",
            schema: "loom",
            table: "projects",
            type: "bit",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsArchived",
            schema: "loom",
            table: "projects");
    }
}
