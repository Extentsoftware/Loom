using System.ComponentModel;
using System.Text.Json.Serialization;
using Loom.Application.Features;
using ModelContextProtocol.Server;

namespace Loom.Mcp.Tools;

/// <summary>
/// Phase-2 MCP tool: <c>loom.search_nodes</c>. Keyword search across node
/// title and intent. Phase-6 will swap the underlying implementation to
/// the Elasticsearch-backed memory service; the tool's wire shape stays
/// the same.
/// </summary>
[McpServerToolType]
public static class SearchNodesTool
{
    [McpServerTool(Name = "loom_search_nodes")]
    [Description("Search Loom feature nodes by keyword across title and intent.")]
    public static async Task<SearchNodesResult> SearchAsync(
        IFeatureService features,
        [Description("Search query.")] string query,
        [Description("Optional project id to scope the search; null searches all projects.")] Guid? projectId = null,
        [Description("Maximum results to return (default 25).")] int take = 25,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        var hits = await features.SearchAsync(query, projectId, take, ct);
        return new SearchNodesResult(
            Query: query,
            Count: hits.Count,
            Hits: [.. hits.Select(h => new SearchHitDto(
                NodeId: h.NodeId.Value,
                ProjectId: h.ProjectId,
                Slug: h.Slug,
                Title: h.Title,
                Type: h.Type.ToString(),
                Phase: h.Phase.ToString(),
                UpdatedAt: h.UpdatedAt))]);
    }

    public sealed record SearchNodesResult(
        [property: JsonPropertyName("query")] string Query,
        [property: JsonPropertyName("count")] int Count,
        [property: JsonPropertyName("hits")] IReadOnlyList<SearchHitDto> Hits);

    public sealed record SearchHitDto(
        [property: JsonPropertyName("node_id")] Guid NodeId,
        [property: JsonPropertyName("project_id")] Guid ProjectId,
        [property: JsonPropertyName("slug")] string Slug,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("phase")] string Phase,
        [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt);
}
