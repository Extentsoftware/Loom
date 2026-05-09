using Loom.Domain.Common;
using Loom.Domain.Nodes;

namespace Loom.Domain.Artifacts;

/// <summary>
/// Identity for a <see cref="ProjectArtifact"/>. Sibling to <see cref="ArtifactId"/>;
/// the two aggregates do not share a key space because they have different
/// lifecycles (project artifacts are flat reference material, design artifacts
/// carry a version chain — see ADR-0013 and ADR-0018).
/// </summary>
public readonly record struct ProjectArtifactId(Guid Value) : IEntityId
{
    public static ProjectArtifactId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString("N");
}

/// <summary>
/// What kind of seed material a <see cref="ProjectArtifact"/> represents.
/// Distinguishes link-only references (Repo, Figma, …) from file uploads
/// (Image, Document, …). Inline + small binary uploads share the File
/// branch; the discriminator drives UI affordances and prompt-assembly
/// rendering rules.
/// </summary>
public enum ProjectArtifactKind
{
    // Links
    Repo = 1,
    Figma = 2,
    Confluence = 3,
    Miro = 4,
    ExternalUrl = 5,
    // Files (bytes stored in the blob store; see ADR-0018)
    Image = 100,
    Document = 101,
    ArchiveBundle = 102
}

/// <summary>
/// Whether a <see cref="ProjectArtifact"/> is addressable as a URL only or
/// carries bytes in the blob store.
/// </summary>
public enum ProjectArtifactPayload
{
    Link = 1,
    File = 2
}

/// <summary>
/// A piece of seed material attached to a project (and optionally narrowed
/// to a specific feature node). Project artifacts are flat reference rows —
/// not versioned, not lockable. See ADR-0018 for why they live alongside
/// <see cref="Artifact"/> rather than reusing it.
///
/// Inheritance is a query concern: feature lookups read both rows where
/// <see cref="NodeId"/> is null (project-scope) and rows where it matches
/// the feature's node id.
/// </summary>
public sealed class ProjectArtifact
{
    private ProjectArtifact() { } // EF Core

    private ProjectArtifact(
        ProjectArtifactId id,
        Guid projectId,
        NodeId? nodeId,
        ProjectArtifactKind kind,
        ProjectArtifactPayload payload,
        string label,
        string? description,
        string? url,
        BlobRef? blob,
        Guid? createdByUserId,
        DateTimeOffset createdAt)
    {
        Id = id;
        ProjectId = projectId;
        NodeId = nodeId;
        Kind = kind;
        Payload = payload;
        Label = label;
        Description = description;
        Url = url;
        Blob = blob;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
    }

    public ProjectArtifactId Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public NodeId? NodeId { get; private set; }
    public ProjectArtifactKind Kind { get; private set; }
    public ProjectArtifactPayload Payload { get; private set; }
    public string Label { get; private set; } = null!;
    public string? Description { get; private set; }
    public string? Url { get; private set; }
    public BlobRef? Blob { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static ProjectArtifact CreateLink(
        Guid projectId,
        NodeId? nodeId,
        ProjectArtifactKind kind,
        string label,
        string url,
        string? description,
        Guid? createdByUserId,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        if (kind is ProjectArtifactKind.Image or ProjectArtifactKind.Document or ProjectArtifactKind.ArchiveBundle)
        {
            throw new DomainException(
                $"ProjectArtifactKind.{kind} carries bytes — use CreateFile, not CreateLink.");
        }

        return new ProjectArtifact(
            ProjectArtifactId.New(),
            projectId,
            nodeId,
            kind,
            ProjectArtifactPayload.Link,
            label.Trim(),
            string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            url.Trim(),
            blob: null,
            createdByUserId,
            now);
    }

    public static ProjectArtifact CreateFile(
        Guid projectId,
        NodeId? nodeId,
        ProjectArtifactKind kind,
        string label,
        BlobRef blob,
        string? description,
        Guid? createdByUserId,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(blob);
        if (kind is not (ProjectArtifactKind.Image or ProjectArtifactKind.Document or ProjectArtifactKind.ArchiveBundle))
        {
            throw new DomainException(
                $"ProjectArtifactKind.{kind} is a link kind — use CreateLink, not CreateFile.");
        }

        return new ProjectArtifact(
            ProjectArtifactId.New(),
            projectId,
            nodeId,
            kind,
            ProjectArtifactPayload.File,
            label.Trim(),
            string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            url: null,
            blob,
            createdByUserId,
            now);
    }
}
