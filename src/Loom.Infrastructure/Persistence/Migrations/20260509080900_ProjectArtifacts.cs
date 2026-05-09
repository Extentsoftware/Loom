using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loom.Infrastructure.Persistence.Migrations;

/// <summary>
/// Adds project-scoped seed artifacts (links + file uploads) and the
/// MessagePack-backed blob store table they reference. See ADR-0018.
/// </summary>
public partial class ProjectArtifacts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "artifact_blobs",
            schema: "loom",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Payload = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                ContentType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_artifact_blobs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "project_artifacts",
            schema: "loom",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                NodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Kind = table.Column<int>(type: "int", nullable: false),
                Payload = table.Column<int>(type: "int", nullable: false),
                Label = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                Url = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                blob_uri = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                blob_content_type = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                blob_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_project_artifacts", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_project_artifacts_ProjectId",
            schema: "loom",
            table: "project_artifacts",
            column: "ProjectId");

        migrationBuilder.CreateIndex(
            name: "IX_project_artifacts_ProjectId_NodeId",
            schema: "loom",
            table: "project_artifacts",
            columns: ProjectIdNodeIdColumns);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "artifact_blobs",
            schema: "loom");

        migrationBuilder.DropTable(
            name: "project_artifacts",
            schema: "loom");
    }

    private static readonly string[] ProjectIdNodeIdColumns = ["ProjectId", "NodeId"];
}
