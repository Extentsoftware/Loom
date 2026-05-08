using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loom.Infrastructure.Persistence.Migrations;

/// <summary>
/// Subscriptions can now scope to a whole project (ProjectId column) and
/// optionally narrow by gating role (Role column). NodeId becomes nullable
/// — exactly one of NodeId / ProjectId is set per row, enforced by the
/// aggregate. The unique index covers the full filter shape so a user can
/// have parallel rows for different roles.
/// </summary>
public partial class SubscriptionsProjectAndRole : Migration
{
    private static readonly string[] ProjectIndexColumns = ["ProjectId", "EventType"];
    private static readonly string[] UniqueColumns = ["UserId", "NodeId", "ProjectId", "EventType", "Channel", "Role"];
    private static readonly string[] LegacyUniqueColumns = ["UserId", "NodeId", "EventType", "Channel"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_subscriptions_UserId_NodeId_EventType_Channel",
            schema: "loom",
            table: "subscriptions");

        migrationBuilder.AlterColumn<System.Guid>(
            name: "NodeId",
            schema: "loom",
            table: "subscriptions",
            type: "uniqueidentifier",
            nullable: true,
            oldClrType: typeof(System.Guid),
            oldType: "uniqueidentifier");

        migrationBuilder.AddColumn<System.Guid>(
            name: "ProjectId",
            schema: "loom",
            table: "subscriptions",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "Role",
            schema: "loom",
            table: "subscriptions",
            type: "int",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_subscriptions_ProjectId_EventType",
            schema: "loom",
            table: "subscriptions",
            columns: ProjectIndexColumns);

        // No filter — SQL Server treats each NULL as distinct in unique
        // indexes by default, which is what we want: a user can have
        // parallel rows where (NodeId, ProjectId) is (X, NULL) vs
        // (NULL, Y) without colliding.
        migrationBuilder.CreateIndex(
            name: "IX_subscriptions_UserId_NodeId_ProjectId_EventType_Channel_Role",
            schema: "loom",
            table: "subscriptions",
            columns: UniqueColumns,
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_subscriptions_ProjectId_EventType",
            schema: "loom",
            table: "subscriptions");

        migrationBuilder.DropIndex(
            name: "IX_subscriptions_UserId_NodeId_ProjectId_EventType_Channel_Role",
            schema: "loom",
            table: "subscriptions");

        migrationBuilder.DropColumn(
            name: "ProjectId",
            schema: "loom",
            table: "subscriptions");

        migrationBuilder.DropColumn(
            name: "Role",
            schema: "loom",
            table: "subscriptions");

        migrationBuilder.AlterColumn<System.Guid>(
            name: "NodeId",
            schema: "loom",
            table: "subscriptions",
            type: "uniqueidentifier",
            nullable: false,
            defaultValue: new System.Guid("00000000-0000-0000-0000-000000000000"),
            oldClrType: typeof(System.Guid),
            oldType: "uniqueidentifier",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_subscriptions_UserId_NodeId_EventType_Channel",
            schema: "loom",
            table: "subscriptions",
            columns: LegacyUniqueColumns,
            unique: true);
    }
}
