using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loom.Infrastructure.Persistence.Migrations;

/// <summary>
/// Persisted in-app notification feed. Written by the InApp channel when
/// a domain event matches a user's subscription, surfaced via the bell
/// icon in the top nav and pushed live over SignalR.
/// </summary>
public partial class AddNotifications : Migration
{
    private static readonly string[] FeedIndexColumns = ["UserId", "ReadAt", "CreatedAt"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "notifications",
            schema: "loom",
            columns: table => new
            {
                Id = table.Column<System.Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<System.Guid>(type: "uniqueidentifier", nullable: false),
                NodeId = table.Column<System.Guid>(type: "uniqueidentifier", nullable: true),
                RunId = table.Column<System.Guid>(type: "uniqueidentifier", nullable: true),
                EventType = table.Column<int>(type: "int", nullable: false),
                Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                DeepLink = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Severity = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<System.DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ReadAt = table.Column<System.DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_notifications", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_notifications_UserId_ReadAt_CreatedAt",
            schema: "loom",
            table: "notifications",
            columns: FeedIndexColumns);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "notifications",
            schema: "loom");
    }
}
