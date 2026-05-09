using Loom.Domain.Artifacts;
using Loom.Domain.Nodes;

namespace Loom.Application.Abstractions;

/// <summary>
/// Read/write access to project-scoped seed artifacts (ADR-0018). Distinct
/// from <see cref="IArtifactRepository"/> because the two aggregates have
/// different lifecycles: design artifacts version + lock; project artifacts
/// are flat reference rows.
/// </summary>
public interface IProjectArtifactRepository
{
    Task<ProjectArtifact?> GetAsync(ProjectArtifactId id, CancellationToken ct = default);

    /// <summary>
    /// Inheritance-aware lookup. Returns project-scope rows (NodeId is null)
    /// unioned with rows attached to <paramref name="nodeId"/> if supplied.
    /// </summary>
    Task<IReadOnlyList<ProjectArtifact>> ListForFeatureAsync(
        Guid projectId,
        NodeId? nodeId,
        CancellationToken ct = default);

    Task AddAsync(ProjectArtifact artifact, CancellationToken ct = default);
    Task RemoveAsync(ProjectArtifact artifact, CancellationToken ct = default);
}
