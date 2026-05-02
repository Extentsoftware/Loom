using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loom.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Initial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "loom");

        // ─── projects ───────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "projects",
            schema: "loom",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Slug = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_projects", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_projects_Slug",
            schema: "loom",
            table: "projects",
            column: "Slug",
            unique: true);

        // ─── nodes ──────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "nodes",
            schema: "loom",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Slug = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Type = table.Column<int>(type: "int", nullable: false),
                Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                Intent = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                Phase = table.Column<int>(type: "int", nullable: false),
                OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                open_questions = table.Column<string>(type: "nvarchar(max)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_nodes", x => x.Id);
                table.ForeignKey(
                    name: "FK_nodes_nodes_ParentId",
                    column: x => x.ParentId,
                    principalSchema: "loom",
                    principalTable: "nodes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_nodes_ParentId",
            schema: "loom",
            table: "nodes",
            column: "ParentId");

        migrationBuilder.CreateIndex(
            name: "IX_nodes_ProjectId",
            schema: "loom",
            table: "nodes",
            column: "ProjectId");

        migrationBuilder.CreateIndex(
            name: "IX_nodes_ProjectId_Slug",
            schema: "loom",
            table: "nodes",
            columns: ["ProjectId", "Slug"],
            unique: true);

        // ─── owned collections of node ──────────────────────────────────
        migrationBuilder.CreateTable(
            name: "node_outcomes",
            schema: "loom",
            columns: table => new
            {
                node_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ordinal = table.Column<int>(type: "int", nullable: false),
                Statement = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                MetricHint = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Measurable = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_node_outcomes", x => new { x.node_id, x.ordinal });
                table.ForeignKey(
                    name: "FK_node_outcomes_nodes_node_id",
                    column: x => x.node_id,
                    principalSchema: "loom",
                    principalTable: "nodes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "node_hypotheses",
            schema: "loom",
            columns: table => new
            {
                node_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ordinal = table.Column<int>(type: "int", nullable: false),
                If = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                Then = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                Because = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_node_hypotheses", x => new { x.node_id, x.ordinal });
                table.ForeignKey(
                    name: "FK_node_hypotheses_nodes_node_id",
                    column: x => x.node_id,
                    principalSchema: "loom",
                    principalTable: "nodes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "node_constraints",
            schema: "loom",
            columns: table => new
            {
                node_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ordinal = table.Column<int>(type: "int", nullable: false),
                Kind = table.Column<int>(type: "int", nullable: false),
                Detail = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_node_constraints", x => new { x.node_id, x.ordinal });
                table.ForeignKey(
                    name: "FK_node_constraints_nodes_node_id",
                    column: x => x.node_id,
                    principalSchema: "loom",
                    principalTable: "nodes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "node_stakeholders",
            schema: "loom",
            columns: table => new
            {
                node_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ordinal = table.Column<int>(type: "int", nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Role = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Interest = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_node_stakeholders", x => new { x.node_id, x.ordinal });
                table.ForeignKey(
                    name: "FK_node_stakeholders_nodes_node_id",
                    column: x => x.node_id,
                    principalSchema: "loom",
                    principalTable: "nodes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        // ─── fragments ──────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "fragments",
            schema: "loom",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Key = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Category = table.Column<int>(type: "int", nullable: false),
                Scope = table.Column<int>(type: "int", nullable: false),
                ScopeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CurrentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                tags = table.Column<string>(type: "nvarchar(max)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_fragments", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_fragments_Category",
            schema: "loom",
            table: "fragments",
            column: "Category");

        migrationBuilder.CreateIndex(
            name: "IX_fragments_Key_Scope_ScopeId",
            schema: "loom",
            table: "fragments",
            columns: ["Key", "Scope", "ScopeId"],
            unique: true,
            filter: null);

        migrationBuilder.CreateTable(
            name: "fragment_versions",
            schema: "loom",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FragmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Version = table.Column<int>(type: "int", nullable: false),
                Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ChangeNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                IsDeprecated = table.Column<bool>(type: "bit", nullable: false),
                hints_prefers_extended_thinking = table.Column<bool>(type: "bit", nullable: false),
                hints_max_context_tokens = table.Column<int>(type: "int", nullable: true),
                hints_requires_json_output = table.Column<bool>(type: "bit", nullable: false),
                hints_requires_filesystem = table.Column<bool>(type: "bit", nullable: false),
                hints_preferred_model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_fragment_versions", x => x.Id);
                table.ForeignKey(
                    name: "FK_fragment_versions_fragments_FragmentId",
                    column: x => x.FragmentId,
                    principalSchema: "loom",
                    principalTable: "fragments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_fragment_versions_FragmentId_Version",
            schema: "loom",
            table: "fragment_versions",
            columns: ["FragmentId", "Version"],
            unique: true);

        // ─── runs ───────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "runs",
            schema: "loom",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                NodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                WorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                StepId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Engine = table.Column<int>(type: "int", nullable: false),
                ExternalRunId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                State = table.Column<int>(type: "int", nullable: false),
                FailureReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                budgets_max_input_tokens = table.Column<int>(type: "int", nullable: true),
                budgets_max_output_tokens = table.Column<int>(type: "int", nullable: true),
                budgets_max_wall_clock = table.Column<TimeSpan>(type: "time", nullable: true),
                budgets_max_cost_usd = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true),
                cost_model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                cost_deployment = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                cost_input_tokens = table.Column<int>(type: "int", nullable: true),
                cost_output_tokens = table.Column<int>(type: "int", nullable: true),
                cost_usd_amount = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_runs", x => x.Id);
            });

        migrationBuilder.CreateIndex(name: "IX_runs_NodeId", schema: "loom", table: "runs", column: "NodeId");
        migrationBuilder.CreateIndex(name: "IX_runs_State", schema: "loom", table: "runs", column: "State");
        migrationBuilder.CreateIndex(name: "IX_runs_CreatedAt", schema: "loom", table: "runs", column: "CreatedAt");

        migrationBuilder.CreateTable(
            name: "run_fragments",
            schema: "loom",
            columns: table => new
            {
                run_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ordinal = table.Column<int>(type: "int", nullable: false),
                FragmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Version = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_run_fragments", x => new { x.run_id, x.ordinal });
                table.ForeignKey(
                    name: "FK_run_fragments_runs_run_id",
                    column: x => x.run_id,
                    principalSchema: "loom",
                    principalTable: "runs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        // ─── artifacts ──────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "artifacts",
            schema: "loom",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                NodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Kind = table.Column<int>(type: "int", nullable: false),
                Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                LastSyncedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                canonical_store = table.Column<int>(type: "int", nullable: false),
                canonical_external_id = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                canonical_url = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_artifacts", x => x.Id);
            });

        migrationBuilder.CreateIndex(name: "IX_artifacts_NodeId", schema: "loom", table: "artifacts", column: "NodeId");
        migrationBuilder.CreateIndex(name: "IX_artifacts_Kind", schema: "loom", table: "artifacts", column: "Kind");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "artifacts", schema: "loom");
        migrationBuilder.DropTable(name: "run_fragments", schema: "loom");
        migrationBuilder.DropTable(name: "runs", schema: "loom");
        migrationBuilder.DropTable(name: "fragment_versions", schema: "loom");
        migrationBuilder.DropTable(name: "fragments", schema: "loom");
        migrationBuilder.DropTable(name: "node_stakeholders", schema: "loom");
        migrationBuilder.DropTable(name: "node_constraints", schema: "loom");
        migrationBuilder.DropTable(name: "node_hypotheses", schema: "loom");
        migrationBuilder.DropTable(name: "node_outcomes", schema: "loom");
        migrationBuilder.DropTable(name: "nodes", schema: "loom");
        migrationBuilder.DropTable(name: "projects", schema: "loom");
    }
}
