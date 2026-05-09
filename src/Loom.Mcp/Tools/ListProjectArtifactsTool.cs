using System.ComponentModel;
using System.Text.Json.Serialization;
using Loom.Application.Artifacts;
using Loom.Domain.Nodes;
using ModelContextProtocol.Server;

namespace Loom.Mcp.Tools;

/// <summary>
/// MCP tool: <c>loom_list_project_artifacts</c>. Returns project-scope seed
/// artifacts unioned with rows attached to a specific feature node when one
/// is supplied. See ADR-0018.
/// </summary>
[McpServerToolType]
public static class ListProjectArtifactsTool
{
    [McpServerTool(Name = "loom_list_project_artifacts")]
    [Description("List the seed artifacts (links + uploaded files) attached to a project, plus any rows narrowed to the given feature node. Files are addressable via loom_get_artifact using the returned blob_uri.")]
    public static async Task<ListProjectArtifactsResult> ListAsync(
        IProjectArtifactService projectArtifacts,
        [Description("Project id to list artifacts for.")] Guid projectId,
        [Description("Optional feature node id to include feature-scoped artifacts in the union.")] Guid? nodeId = null,
        CancellationToken ct = default)
    {
        NodeId? scoped = nodeId is Guid g ? new NodeId(g) : null;
        var rows = await projectArtifacts.ListForFeatureAsync(projectId, scoped, ct);

        var dtos = rows
            .Select(a => new ProjectArtifactDto(
                Id: a.Id.Value,
                Kind: a.Kind.ToString(),
                Payload: a.Payload.ToString(),
                Label: a.Label,
                Description: a.Description,
                Url: a.Url,
                BlobUri: a.Blob?.Uri,
                ContentType: a.Blob?.ContentType,
                SizeBytes: a.Blob?.SizeBytes,
                NodeId: a.NodeId?.Value,
                CreatedAt: a.CreatedAt))
            .ToList();

        return new ListProjectArtifactsResult(projectId, nodeId, dtos.Count, dtos);
    }

    public sealed record ListProjectArtifactsResult(
        [property: JsonPropertyName("project_id")] Guid ProjectId,
        [property: JsonPropertyName("node_id")] Guid? NodeId,
        [property: JsonPropertyName("count")] int Count,
        [property: JsonPropertyName("artifacts")] IReadOnlyList<ProjectArtifactDto> Artifacts);

    public sealed record ProjectArtifactDto(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("kind")] string Kind,
        [property: JsonPropertyName("payload")] string Payload,
        [property: JsonPropertyName("label")] string Label,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("blob_uri")] string? BlobUri,
        [property: JsonPropertyName("content_type")] string? ContentType,
        [property: JsonPropertyName("size_bytes")] long? SizeBytes,
        [property: JsonPropertyName("node_id")] Guid? NodeId,
        [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);
}
