using Loom.Application.Abstractions;
using Loom.Domain.Artifacts;
using Loom.Domain.Common;
using Loom.Domain.Nodes;
using Microsoft.Extensions.Options;

namespace Loom.Application.Artifacts;

public sealed class ProjectArtifactService(
    IProjectArtifactRepository artifacts,
    IArtifactBlobStore blobStore,
    IUnitOfWork uow,
    ISystemClock clock,
    IOptions<ProjectArtifactOptions> options) : IProjectArtifactService
{
    private readonly ProjectArtifactOptions _opts = options.Value;

    public async Task<ProjectArtifact> AttachLinkAsync(
        Guid projectId,
        NodeId? nodeId,
        ProjectArtifactKind kind,
        string label,
        string url,
        string? description,
        Guid? createdByUserId,
        CancellationToken ct = default)
    {
        var artifact = ProjectArtifact.CreateLink(
            projectId, nodeId, kind, label, url, description, createdByUserId, clock.UtcNow);
        await artifacts.AddAsync(artifact, ct);
        await uow.SaveChangesAsync(ct);
        return artifact;
    }

    public async Task<ProjectArtifact> AttachFileAsync(
        Guid projectId,
        NodeId? nodeId,
        ProjectArtifactKind kind,
        string label,
        ArtifactBundle bundle,
        string? description,
        Guid? createdByUserId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        if (bundle.Files.Count == 0)
        {
            throw new DomainException("File bundle must contain at least one file.");
        }

        long total = 0;
        foreach (var f in bundle.Files)
        {
            if (f.Bytes.LongLength > _opts.MaxFileBytes)
            {
                throw new DomainException(
                    $"File '{f.Filename}' is {f.Bytes.LongLength} bytes; max {_opts.MaxFileBytes}.");
            }
            total += f.Bytes.LongLength;
        }
        if (total > _opts.MaxBundleBytes)
        {
            throw new DomainException(
                $"Bundle is {total} bytes; max {_opts.MaxBundleBytes}.");
        }

        var blobRef = await blobStore.PutAsync(bundle, ct);
        var artifact = ProjectArtifact.CreateFile(
            projectId, nodeId, kind, label, blobRef, description, createdByUserId, clock.UtcNow);
        await artifacts.AddAsync(artifact, ct);
        await uow.SaveChangesAsync(ct);
        return artifact;
    }

    public Task<IReadOnlyList<ProjectArtifact>> ListForFeatureAsync(
        Guid projectId,
        NodeId? nodeId,
        CancellationToken ct = default) =>
        artifacts.ListForFeatureAsync(projectId, nodeId, ct);

    public async Task RemoveAsync(ProjectArtifactId id, CancellationToken ct = default)
    {
        var artifact = await artifacts.GetAsync(id, ct);
        if (artifact is null)
        {
            return;
        }

        var blobUri = artifact.Blob?.Uri;
        await artifacts.RemoveAsync(artifact, ct);
        await uow.SaveChangesAsync(ct);
        if (!string.IsNullOrEmpty(blobUri))
        {
            await blobStore.DeleteAsync(blobUri, ct);
        }
    }
}
