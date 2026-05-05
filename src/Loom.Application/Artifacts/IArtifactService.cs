using Loom.Domain.Annotations;
using Loom.Domain.Artifacts;
using Loom.Domain.Nodes;

namespace Loom.Application.Artifacts;

/// <summary>
/// Application surface for artifact lifecycle: creation, version publication,
/// soft locks, annotation. Pure pass-through to the aggregate plus the
/// infrastructure plumbing (repository + UoW + clock).
/// </summary>
public interface IArtifactService
{
    Task<Artifact> CreateAsync(
        NodeId nodeId,
        ArtifactKind kind,
        string title,
        CanonicalPointer canonical,
        CancellationToken ct = default);

    /// <summary>
    /// Replace the canonical pointer of an existing artifact. Used when a
    /// step-output projector re-runs and the body needs refreshing.
    /// </summary>
    Task UpdateCanonicalAsync(
        ArtifactId artifactId,
        CanonicalPointer canonical,
        CancellationToken ct = default);

    Task<ArtifactVersion> PublishVersionAsync(
        ArtifactId artifactId,
        Author author,
        BlobRef content,
        BlobRef? preview,
        string? reason,
        CancellationToken ct = default);

    Task AcquireLockAsync(
        ArtifactId artifactId,
        Guid userId,
        TimeSpan duration,
        CancellationToken ct = default);

    Task ReleaseLockAsync(
        ArtifactId artifactId,
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Force-release a lock regardless of holder. The application service is
    /// responsible for verifying the caller is a project admin; the
    /// aggregate trusts that check has happened.
    /// </summary>
    Task ForceReleaseLockAsync(ArtifactId artifactId, CancellationToken ct = default);

    Task<Annotation> AddAnnotationAsync(
        ArtifactId artifactId,
        Guid? userId,
        AnnotationKind kind,
        AnnotationTarget? target,
        string body,
        IEnumerable<string>? tags,
        CancellationToken ct = default);

    Task<IReadOnlyList<Annotation>> ListAnnotationsAsync(
        ArtifactId artifactId,
        CancellationToken ct = default);
}
