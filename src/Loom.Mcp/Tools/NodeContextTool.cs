using System.ComponentModel;
using System.Text.Json.Serialization;
using Loom.Application.Abstractions;
using Loom.Application.Fragments;
using Loom.Domain.Common;
using Loom.Domain.Fragments;
using Loom.Domain.Nodes;
using Loom.Domain.Workflows;
using ModelContextProtocol.Server;

namespace Loom.Mcp.Tools;

/// <summary>
/// Phase-2 MCP tool: <c>loom.get_node_context</c>. Resolves a node by its
/// project-qualified slug (e.g. "feature/checkout-redesign" — slug is
/// project-unique) or by GUID, returns the live node fields plus the
/// effective fragment set keyed against a small Phase-2 selector list.
///
/// The Phase-2 cut keeps the selector list hardcoded (mirroring the kickoff
/// "discovery" step). Phase 5 lets clients pass selectors directly.
/// </summary>
[McpServerToolType]
public static class NodeContextTool
{
    [McpServerTool(Name = "loom_get_node_context")]
    [Description("Get the assembled context (node fields + effective fragments) for a Loom feature node, identified either by GUID or by project-unique slug.")]
    public static async Task<NodeContextResult> GetNodeContextAsync(
        IFeatureNodeRepository nodes,
        IProjectRepository projects,
        IFragmentService fragmentService,
        [Description("Node identifier — either a GUID or a project-unique slug.")] string nodeRef,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeRef);

        FeatureNode? node = null;
        if (Guid.TryParse(nodeRef, out var guid))
        {
            node = await nodes.GetAsync(new NodeId(guid), ct);
        }
        else if (Slug.TryFrom(nodeRef, out _))
        {
            // Slug is project-unique, not globally unique — search all
            // projects. Phase 5 will accept a "{project-slug}/{node-slug}"
            // qualifier; Phase 2 keeps the resolver dumb.
            var allProjects = await projects.ListAsync(ct);
            foreach (var p in allProjects)
            {
                var byProject = await nodes.GetByProjectAsync(p.Id, ct);
                node = byProject.FirstOrDefault(n => n.Slug.Value == nodeRef);
                if (node is not null)
                {
                    break;
                }
            }
        }

        if (node is null)
        {
            return new NodeContextResult(false, $"No node found for '{nodeRef}'.", null);
        }

        var selectors = DefaultSelectors();
        var effective = await fragmentService.GetEffectiveFragmentsAsync(node.Id, selectors, ct);

        var dto = new NodeContextDto(
            NodeId: node.Id.Value,
            ProjectId: node.ProjectId,
            Slug: node.Slug.Value,
            Title: node.Title,
            Type: node.Type.ToString(),
            Phase: node.Phase.ToString(),
            Intent: node.Intent,
            Outcomes: [.. node.Outcomes.Select(o => o.Statement)],
            OpenQuestions: [.. node.OpenQuestions],
            EffectiveFragments: [.. effective.Select(f => new EffectiveFragmentDto(
                Key: f.Fragment.Key.Value,
                Category: f.Fragment.Category.ToString(),
                Source: f.Source.ToString(),
                Version: f.Version.Version,
                Content: f.Version.Content))]);

        return new NodeContextResult(true, null, dto);
    }

    private static IReadOnlyList<FragmentSelector> DefaultSelectors() =>
    [
        new(FragmentCategory.Identity, Slug.From("po-discovery-assistant")),
        new(FragmentCategory.Methodology, Slug.From("problem-framing")),
        new(FragmentCategory.Methodology, Slug.From("definition-of-ready"))
    ];

    public sealed record NodeContextResult(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("error")] string? Error,
        [property: JsonPropertyName("node")] NodeContextDto? Node);

    public sealed record NodeContextDto(
        [property: JsonPropertyName("node_id")] Guid NodeId,
        [property: JsonPropertyName("project_id")] Guid ProjectId,
        [property: JsonPropertyName("slug")] string Slug,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("phase")] string Phase,
        [property: JsonPropertyName("intent")] string? Intent,
        [property: JsonPropertyName("outcomes")] IReadOnlyList<string> Outcomes,
        [property: JsonPropertyName("open_questions")] IReadOnlyList<string> OpenQuestions,
        [property: JsonPropertyName("effective_fragments")] IReadOnlyList<EffectiveFragmentDto> EffectiveFragments);

    public sealed record EffectiveFragmentDto(
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("category")] string Category,
        [property: JsonPropertyName("source")] string Source,
        [property: JsonPropertyName("version")] int Version,
        [property: JsonPropertyName("content")] string Content);
}
