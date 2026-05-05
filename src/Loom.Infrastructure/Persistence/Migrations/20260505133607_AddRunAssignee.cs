using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loom.Infrastructure.Persistence.Migrations;

/// <summary>
/// Adds an optional assignee (Loom user id + assigned-at timestamp) to
/// <c>runs</c>. Drives the MCP claim path so a developer's Claude Code
/// instance can pull the next gated task and report back. Index covers
/// the "what's queued for me?" lookup.
/// </summary>
public partial class AddRunAssignee : Migration
{
    private static readonly string[] AssigneeIndexColumns = ["AssigneeUserId", "State"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<System.DateTimeOffset>(
            name: "AssignedAt",
            schema: "loom",
            table: "runs",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<System.Guid>(
            name: "AssigneeUserId",
            schema: "loom",
            table: "runs",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_runs_AssigneeUserId_State",
            schema: "loom",
            table: "runs",
            columns: AssigneeIndexColumns);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_runs_AssigneeUserId_State",
            schema: "loom",
            table: "runs");

        migrationBuilder.DropColumn(
            name: "AssignedAt",
            schema: "loom",
            table: "runs");

        migrationBuilder.DropColumn(
            name: "AssigneeUserId",
            schema: "loom",
            table: "runs");
    }
}
