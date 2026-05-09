using Loom.Domain.Artifacts;
using Loom.Domain.Nodes;

namespace Loom.Application.Artifacts;

/// <summary>
/// Application surface for project-scoped seed artifacts (ADR-0018).
/// Coordinates the repository, the blob store, and the unit of work so
/// links and file uploads share a single transaction boundary.
/// </summary>
public interface IProjectArtifactService
{
    Task<ProjectArtifact> AttachLinkAsync(
        Guid projectId,
        NodeId? nodeId,
        ProjectArtifactKind kind,
        string label,
        string url,
        string? description,
        Guid? createdByUserId,
        CancellationToken ct = default);

    Task<ProjectArtifact> AttachFileAsync(
        Guid projectId,
        NodeId? nodeId,
        ProjectArtifactKind kind,
        string label,
        ArtifactBundle bundle,
        string? description,
        Guid? createdByUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Inheritance-aware: returns project-scope rows plus rows attached to
    /// <paramref name="nodeId"/> when supplied.
    /// </summary>
    Task<IReadOnlyList<ProjectArtifact>> ListForFeatureAsync(
        Guid projectId,
        NodeId? nodeId,
        CancellationToken ct = default);

    Task RemoveAsync(ProjectArtifactId id, CancellationToken ct = default);
}
