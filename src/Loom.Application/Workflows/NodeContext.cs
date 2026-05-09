using Loom.Domain.Artifacts;
using Loom.Domain.Nodes;

namespace Loom.Application.Workflows;

/// <summary>
/// Snapshot of the live node fields and ancestor chain that's fed into prompt
/// assembly as the auto-injected "context" fragment. Pure data — the composer
/// renders it into the system prompt; nothing here mutates.
/// </summary>
public sealed record NodeContext(
    NodeId NodeId,
    string Title,
    string? Intent,
    NodePhase Phase,
    NodeType Type,
    IReadOnlyList<string> AncestorTitles,
    IReadOnlyList<string> OpenQuestions,
    IReadOnlyList<Outcome> Outcomes,
    IReadOnlyList<Hypothesis> Hypotheses,
    IReadOnlyList<ProjectArtifactSummary> ProjectArtifacts);

/// <summary>
/// Compact view of a project-scoped seed artifact (ADR-0018) for inclusion
/// in prompt assembly. Bytes are not inlined — agents that want the payload
/// fetch it via the MCP <c>get_artifact</c> tool using <see cref="BlobUri"/>.
/// </summary>
public sealed record ProjectArtifactSummary(
    ProjectArtifactId Id,
    ProjectArtifactKind Kind,
    ProjectArtifactPayload Payload,
    string Label,
    string? Description,
    string? Url,
    string? BlobUri,
    string? ContentType);
