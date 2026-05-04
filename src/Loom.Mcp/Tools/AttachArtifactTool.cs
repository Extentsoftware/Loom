using System.ComponentModel;
using System.Text.Json.Serialization;
using Loom.Application.Abstractions;
using Loom.Application.Artifacts;
using Loom.Domain.Artifacts;
using Loom.Domain.Common;
using Loom.Domain.Nodes;
using ModelContextProtocol.Server;

namespace Loom.Mcp.Tools;

/// <summary>
/// Phase-2 MCP write tool: <c>loom.attach_artifact</c>. Creates an Artifact
/// on the target node pointing at a canonical-store URL (Figma, Git,
/// Confluence, Miro, or hub-native). Used from IDE flows like
/// "Cursor, attach this PR to the saved-card-surfacing node" where the
/// canonical bytes live elsewhere and Loom just records the pointer.
///
/// Per the design's auth model, write tools should require explicit user
/// confirmation in the host UI for sensitive operations. This tool's
/// effect is additive (creates a new artifact descriptor; does not mutate
/// existing ones), so it lands at "informational" rather than "destructive"
/// — the calling agent should still echo intent back to the user before
/// invoking it, but Loom doesn't gate per-call.
/// </summary>
[McpServerToolType]
public static class AttachArtifactTool
{
    [McpServerTool(Name = "loom_attach_artifact")]
    [Description("Attach an Artifact to a Loom feature node. The artifact is a pointer to canonical content (a Figma file, a Git blob, a Confluence page, etc.). Returns the new artifact id.")]
    public static async Task<AttachArtifactResult> AttachArtifactAsync(
        IFeatureNodeRepository nodes,
        IProjectRepository projects,
        IArtifactService artifacts,
        [Description("Node identifier — either a GUID or a project-unique slug.")] string nodeRef,
        [Description("Artifact kind. One of: Criteria, Wireframe, Code, Diagram, TestPlan, Adr, Doc.")] string kind,
        [Description("Short human-readable title for the artifact.")] string title,
        [Description("Canonical store. One of: HubNative, Figma, Git, Miro, Confluence.")] string store,
        [Description("URL where the canonical content lives. Required for non-HubNative stores unless externalId is provided.")] string? url = null,
        [Description("External identifier in the canonical store (Figma node id, git sha, Confluence page id, …). Optional when a URL is provided.")] string? externalId = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeRef);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(store);

        if (!Enum.TryParse<ArtifactKind>(kind, ignoreCase: true, out var artifactKind))
        {
            return new AttachArtifactResult(false,
                $"Unknown artifact kind '{kind}'. Valid: {string.Join(", ", Enum.GetNames<ArtifactKind>())}.",
                null);
        }
        if (!Enum.TryParse<CanonicalStore>(store, ignoreCase: true, out var canonicalStore))
        {
            return new AttachArtifactResult(false,
                $"Unknown canonical store '{store}'. Valid: {string.Join(", ", Enum.GetNames<CanonicalStore>())}.",
                null);
        }

        var node = await ResolveNodeAsync(nodes, projects, nodeRef, ct);
        if (node is null)
        {
            return new AttachArtifactResult(false, $"No node found for '{nodeRef}'.", null);
        }

        var trimmedUrl = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
        var trimmedExternalId = string.IsNullOrWhiteSpace(externalId) ? null : externalId.Trim();

        if (canonicalStore != CanonicalStore.HubNative
            && trimmedUrl is null && trimmedExternalId is null)
        {
            return new AttachArtifactResult(false,
                "Provide a URL or external id for non-hub-native artifacts.",
                null);
        }

        var pointer = new CanonicalPointer(
            Store: canonicalStore,
            ExternalId: trimmedExternalId ?? trimmedUrl ?? string.Empty,
            Url: trimmedUrl);

        var artifact = await artifacts.CreateAsync(node.Id, artifactKind, title.Trim(), pointer, ct);

        return new AttachArtifactResult(true, null, new AttachedArtifactDto(
            ArtifactId: artifact.Id.Value,
            NodeId: node.Id.Value,
            Kind: artifact.Kind.ToString(),
            Title: artifact.Title,
            Store: artifact.Canonical.Store.ToString(),
            Url: artifact.Canonical.Url,
            ExternalId: artifact.Canonical.ExternalId));
    }

    private static async Task<FeatureNode?> ResolveNodeAsync(
        IFeatureNodeRepository nodes,
        IProjectRepository projects,
        string nodeRef,
        CancellationToken ct)
    {
        if (Guid.TryParse(nodeRef, out var guid))
        {
            return await nodes.GetAsync(new NodeId(guid), ct);
        }
        if (Slug.TryFrom(nodeRef, out _))
        {
            // Same project-walk fallback as NodeContextTool until Phase 5
            // accepts qualified "{project-slug}/{node-slug}" addresses.
            var allProjects = await projects.ListAsync(ct);
            foreach (var p in allProjects)
            {
                var byProject = await nodes.GetByProjectAsync(p.Id, ct);
                var match = byProject.FirstOrDefault(n => n.Slug.Value == nodeRef);
                if (match is not null)
                {
                    return match;
                }
            }
        }
        return null;
    }

    public sealed record AttachArtifactResult(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("error")] string? Error,
        [property: JsonPropertyName("artifact")] AttachedArtifactDto? Artifact);

    public sealed record AttachedArtifactDto(
        [property: JsonPropertyName("artifact_id")] Guid ArtifactId,
        [property: JsonPropertyName("node_id")] Guid NodeId,
        [property: JsonPropertyName("kind")] string Kind,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("store")] string Store,
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("external_id")] string ExternalId);
}
