using Loom.Application.Abstractions;
using Loom.Domain.Annotations;
using Loom.Domain.Artifacts;
using Loom.Domain.Common;
using Loom.Domain.Nodes;

namespace Loom.Application.Artifacts;

public sealed class ArtifactService(
    IArtifactRepository artifacts,
    IAnnotationRepository annotations,
    IUnitOfWork uow,
    ISystemClock clock) : IArtifactService
{
    public async Task<Artifact> CreateAsync(
        NodeId nodeId,
        ArtifactKind kind,
        string title,
        CanonicalPointer canonical,
        CancellationToken ct = default)
    {
        var artifact = Artifact.Create(nodeId, kind, title, canonical, clock.UtcNow);
        await artifacts.AddAsync(artifact, ct);
        await uow.SaveChangesAsync(ct);
        return artifact;
    }

    public async Task UpdateCanonicalAsync(
        ArtifactId artifactId,
        CanonicalPointer canonical,
        CancellationToken ct = default)
    {
        var artifact = await GetOrThrow(artifactId, ct);
        artifact.UpdateCanonical(canonical, clock.UtcNow);
        await uow.SaveChangesAsync(ct);
    }

    public async Task<ArtifactVersion> PublishVersionAsync(
        ArtifactId artifactId,
        Author author,
        BlobRef content,
        BlobRef? preview,
        string? reason,
        CancellationToken ct = default)
    {
        var artifact = await GetOrThrow(artifactId, ct);
        var version = artifact.PublishVersion(author, content, preview, reason, clock.UtcNow);
        await uow.SaveChangesAsync(ct);
        return version;
    }

    public async Task AcquireLockAsync(
        ArtifactId artifactId,
        Guid userId,
        TimeSpan duration,
        CancellationToken ct = default)
    {
        var artifact = await GetOrThrow(artifactId, ct);
        artifact.AcquireLock(userId, duration, clock.UtcNow);
        await uow.SaveChangesAsync(ct);
    }

    public async Task ReleaseLockAsync(ArtifactId artifactId, Guid userId, CancellationToken ct = default)
    {
        var artifact = await GetOrThrow(artifactId, ct);
        artifact.ReleaseLock(userId, clock.UtcNow);
        await uow.SaveChangesAsync(ct);
    }

    public async Task ForceReleaseLockAsync(ArtifactId artifactId, CancellationToken ct = default)
    {
        var artifact = await GetOrThrow(artifactId, ct);
        artifact.ForceReleaseLock(clock.UtcNow);
        await uow.SaveChangesAsync(ct);
    }

    public async Task<Annotation> AddAnnotationAsync(
        ArtifactId artifactId,
        Guid? userId,
        AnnotationKind kind,
        AnnotationTarget? target,
        string body,
        IEnumerable<string>? tags,
        CancellationToken ct = default)
    {
        // Ensure the artifact exists; the annotation references it by Guid.
        _ = await GetOrThrow(artifactId, ct);
        var ann = Annotation.Create(artifactId.Value, userId, kind, target, body, tags, clock.UtcNow);
        await annotations.AddAsync(ann, ct);
        await uow.SaveChangesAsync(ct);
        return ann;
    }

    public Task<IReadOnlyList<Annotation>> ListAnnotationsAsync(ArtifactId artifactId, CancellationToken ct = default) =>
        annotations.GetByArtifactAsync(artifactId.Value, ct);

    private async Task<Artifact> GetOrThrow(ArtifactId id, CancellationToken ct) =>
        await artifacts.GetAsync(id, ct)
            ?? throw new DomainException($"Artifact {id} not found.");
}
