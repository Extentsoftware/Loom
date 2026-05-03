using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loom.Infrastructure.Persistence.Migrations;

/// <summary>
/// Phase 2 migration: external-integration registry plus an append-only
/// inbound-webhook audit log. Strictly additive — no existing tables touched.
/// </summary>
public partial class IntegrationConnectionsWebhooks : Migration
{
    private static readonly string[] IntegrationKindProjectId = ["Kind", "ProjectId"];
    private static readonly string[] WebhookConnectionIdReceivedAt = ["ConnectionId", "ReceivedAt"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "integration_connections",
            schema: "loom",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Kind = table.Column<int>(type: "int", nullable: false),
                DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                TenantOrAccount = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                SecretRef = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                WebhookSecret = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Status = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                LastHealthCheckAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_integration_connections", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_integration_connections_Kind_ProjectId",
            schema: "loom",
            table: "integration_connections",
            columns: IntegrationKindProjectId);

        migrationBuilder.CreateTable(
            name: "webhook_deliveries",
            schema: "loom",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Kind = table.Column<int>(type: "int", nullable: false),
                EventType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                HeadersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                BodyJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                FailureReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_webhook_deliveries", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_webhook_deliveries_ConnectionId_ReceivedAt",
            schema: "loom",
            table: "webhook_deliveries",
            columns: WebhookConnectionIdReceivedAt);

        migrationBuilder.CreateIndex(
            name: "IX_webhook_deliveries_ReceivedAt",
            schema: "loom",
            table: "webhook_deliveries",
            column: "ReceivedAt");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "webhook_deliveries", schema: "loom");
        migrationBuilder.DropTable(name: "integration_connections", schema: "loom");
    }
}
