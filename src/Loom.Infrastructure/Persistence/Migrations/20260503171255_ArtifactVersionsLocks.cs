using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loom.Infrastructure.Persistence.Migrations;

/// <summary>
/// Phase 4 migration: artifact version chain (immutable, monotonic per
/// artifact) plus soft-lock columns on the artifacts table. Strictly
/// additive on top of 20260503164229. Existing artifacts (none in
/// production yet) will report no Versions and Lock = null until callers
/// publish their first version through ArtifactService.
/// </summary>
public partial class ArtifactVersionsLocks : Migration
{
    private static readonly string[] ArtifactVersionUniqueColumns = ["ArtifactId", "VersionNumber"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<System.DateTimeOffset>(
            name: "lock_acquired_at",
            schema: "loom",
            table: "artifacts",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<System.DateTimeOffset>(
            name: "lock_expires_at",
            schema: "loom",
            table: "artifacts",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<System.Guid>(
            name: "lock_holder_user_id",
            schema: "loom",
            table: "artifacts",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "artifact_versions",
            schema: "loom",
            columns: table => new
            {
                Id = table.Column<System.Guid>(type: "uniqueidentifier", nullable: false),
                ArtifactId = table.Column<System.Guid>(type: "uniqueidentifier", nullable: false),
                VersionNumber = table.Column<int>(type: "int", nullable: false),
                author_kind = table.Column<int>(type: "int", nullable: false),
                author_id = table.Column<System.Guid>(type: "uniqueidentifier", nullable: false),
                content_uri = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                content_type = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                content_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                preview_uri = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                preview_content_type = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                preview_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                CreatedAt = table.Column<System.DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_artifact_versions", x => x.Id);
                table.ForeignKey(
                    name: "FK_artifact_versions_artifacts_ArtifactId",
                    column: x => x.ArtifactId,
                    principalSchema: "loom",
                    principalTable: "artifacts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_artifact_versions_ArtifactId_VersionNumber",
            schema: "loom",
            table: "artifact_versions",
            columns: ArtifactVersionUniqueColumns,
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "artifact_versions",
            schema: "loom");

        migrationBuilder.DropColumn(
            name: "lock_acquired_at",
            schema: "loom",
            table: "artifacts");

        migrationBuilder.DropColumn(
            name: "lock_expires_at",
            schema: "loom",
            table: "artifacts");

        migrationBuilder.DropColumn(
            name: "lock_holder_user_id",
            schema: "loom",
            table: "artifacts");
    }
}
