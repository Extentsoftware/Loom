using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loom.Infrastructure.Persistence.Migrations;

/// <summary>
/// Adds the Phase-1 kernel tables on top of 20260502_Initial: Workflow
/// (and its owned WorkflowSteps with JSON-stored selectors), AssembledPrompt,
/// RunEvent (append-only audit), Transcript (raw + normalized text), and
/// the Outbox table (monotonic Sequence column for indexer ordering).
/// Also adds the AssembledPromptId column to runs so a Run can pin its
/// composed prompt without a separate join.
/// </summary>
public partial class Workflow : Migration
{
    private static readonly string[] WorkflowsKeyVersion = ["Key", "Version"];
    private static readonly string[] WorkflowStepsWorkflowIdOrder = ["WorkflowId", "Order"];
    private static readonly string[] RunEventsRunIdSequence = ["RunId", "Sequence"];
    private static readonly string[] OutboxProcessedAtOccurredAt = ["ProcessedAt", "OccurredAt"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── runs.AssembledPromptId ───────────────────────────────────
        migrationBuilder.AddColumn<Guid>(
            name: "AssembledPromptId",
            schema: "loom",
            table: "runs",
            type: "uniqueidentifier",
            nullable: true);

        // ── workflows ────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "workflows",
            schema: "loom",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Key = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Version = table.Column<int>(type: "int", nullable: false),
                Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_workflows", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_workflows_Key_Version",
            schema: "loom",
            table: "workflows",
            columns: WorkflowsKeyVersion,
            unique: true);

        // ── workflow_steps ───────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "workflow_steps",
            schema: "loom",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                WorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Order = table.Column<int>(type: "int", nullable: false),
                Key = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Kind = table.Column<int>(type: "int", nullable: false),
                Gating = table.Column<int>(type: "int", nullable: false),
                EnginePref = table.Column<int>(type: "int", nullable: true),
                OutputSchemaName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                budgets_max_input_tokens = table.Column<int>(type: "int", nullable: true),
                budgets_max_output_tokens = table.Column<int>(type: "int", nullable: true),
                budgets_max_wall_clock = table.Column<TimeSpan>(type: "time", nullable: true),
                budgets_max_cost_usd = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                selectors = table.Column<string>(type: "nvarchar(max)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_workflow_steps", x => x.Id);
                table.ForeignKey(
                    name: "FK_workflow_steps_workflows_WorkflowId",
                    column: x => x.WorkflowId,
                    principalSchema: "loom",
                    principalTable: "workflows",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_workflow_steps_WorkflowId_Order",
            schema: "loom",
            table: "workflow_steps",
            columns: WorkflowStepsWorkflowIdOrder);

        // ── assembled_prompts ────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "assembled_prompts",
            schema: "loom",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SystemPrompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                OutputSchemaJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                AssembledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                fragments = table.Column<string>(type: "nvarchar(max)", nullable: false),
                messages = table.Column<string>(type: "nvarchar(max)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_assembled_prompts", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_assembled_prompts_RunId",
            schema: "loom",
            table: "assembled_prompts",
            column: "RunId",
            unique: true);

        // ── run_events ───────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "run_events",
            schema: "loom",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Sequence = table.Column<int>(type: "int", nullable: false),
                Kind = table.Column<int>(type: "int", nullable: false),
                PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                At = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_run_events", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_run_events_RunId_Sequence",
            schema: "loom",
            table: "run_events",
            columns: RunEventsRunIdSequence,
            unique: true);

        // ── transcripts ──────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "transcripts",
            schema: "loom",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RawText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                NormalizedJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_transcripts", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_transcripts_RunId",
            schema: "loom",
            table: "transcripts",
            column: "RunId",
            unique: true);

        // ── outbox ───────────────────────────────────────────────────
        // Sequence is an IDENTITY column — monotonic per insert order.
        // Phase 6's Elasticsearch indexer reads rows in Sequence order;
        // this column is the contract.
        migrationBuilder.CreateTable(
            name: "outbox",
            schema: "loom",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Sequence = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                EventType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                Attempts = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_outbox", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_outbox_Sequence",
            schema: "loom",
            table: "outbox",
            column: "Sequence",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_outbox_ProcessedAt_OccurredAt",
            schema: "loom",
            table: "outbox",
            columns: OutboxProcessedAtOccurredAt);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "outbox", schema: "loom");
        migrationBuilder.DropTable(name: "transcripts", schema: "loom");
        migrationBuilder.DropTable(name: "run_events", schema: "loom");
        migrationBuilder.DropTable(name: "assembled_prompts", schema: "loom");
        migrationBuilder.DropTable(name: "workflow_steps", schema: "loom");
        migrationBuilder.DropTable(name: "workflows", schema: "loom");
        migrationBuilder.DropColumn(name: "AssembledPromptId", schema: "loom", table: "runs");
    }
}
