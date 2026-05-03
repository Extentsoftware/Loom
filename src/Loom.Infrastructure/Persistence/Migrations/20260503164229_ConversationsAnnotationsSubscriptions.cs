using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loom.Infrastructure.Persistence.Migrations;

/// <summary>
/// Phase 3 migration: AI conversation persistence (conversations + messages),
/// annotation schema (UI lands in Phase 4), and per-user notification
/// subscriptions. Strictly additive on top of 20260503160628.
/// </summary>
public partial class ConversationsAnnotationsSubscriptions : Migration
{
    private static readonly string[] MessagesConversationIdCreatedAt = ["ConversationId", "CreatedAt"];
    private static readonly string[] SubscriptionsNodeIdEventType = ["NodeId", "EventType"];
    private static readonly string[] SubscriptionsUserIdNodeIdEventTypeChannel = ["UserId", "NodeId", "EventType", "Channel"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "conversations",
            schema: "loom",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                NodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Topic = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_conversations", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_conversations_NodeId",
            schema: "loom",
            table: "conversations",
            column: "NodeId");

        migrationBuilder.CreateIndex(
            name: "IX_conversations_RunId",
            schema: "loom",
            table: "conversations",
            column: "RunId");

        migrationBuilder.CreateTable(
            name: "messages",
            schema: "loom",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                author_kind = table.Column<int>(type: "int", nullable: false),
                author_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                author_run_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_messages", x => x.Id);
                table.ForeignKey(
                    name: "FK_messages_conversations_ConversationId",
                    column: x => x.ConversationId,
                    principalSchema: "loom",
                    principalTable: "conversations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_messages_ConversationId_CreatedAt",
            schema: "loom",
            table: "messages",
            columns: MessagesConversationIdCreatedAt);

        migrationBuilder.CreateTable(
            name: "annotations",
            schema: "loom",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ArtifactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Kind = table.Column<int>(type: "int", nullable: false),
                target_kind = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                target_locator = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                tags = table.Column<string>(type: "nvarchar(max)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_annotations", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_annotations_ArtifactId",
            schema: "loom",
            table: "annotations",
            column: "ArtifactId");

        migrationBuilder.CreateIndex(
            name: "IX_annotations_CreatedAt",
            schema: "loom",
            table: "annotations",
            column: "CreatedAt");

        migrationBuilder.CreateTable(
            name: "subscriptions",
            schema: "loom",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                NodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EventType = table.Column<int>(type: "int", nullable: false),
                Channel = table.Column<int>(type: "int", nullable: false),
                Mode = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_subscriptions", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_subscriptions_NodeId_EventType",
            schema: "loom",
            table: "subscriptions",
            columns: SubscriptionsNodeIdEventType);

        migrationBuilder.CreateIndex(
            name: "IX_subscriptions_UserId_NodeId_EventType_Channel",
            schema: "loom",
            table: "subscriptions",
            columns: SubscriptionsUserIdNodeIdEventTypeChannel,
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "messages", schema: "loom");
        migrationBuilder.DropTable(name: "annotations", schema: "loom");
        migrationBuilder.DropTable(name: "subscriptions", schema: "loom");
        migrationBuilder.DropTable(name: "conversations", schema: "loom");
    }
}
