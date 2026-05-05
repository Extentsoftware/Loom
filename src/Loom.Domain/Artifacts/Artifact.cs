using Loom.Domain.Common;
using Loom.Domain.Nodes;

namespace Loom.Domain.Artifacts;

public readonly record struct ArtifactId(Guid Value) : IEntityId
{
    public static ArtifactId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString("N");
}

public readonly record struct ArtifactVersionId(Guid Value) : IEntityId
{
    public static ArtifactVersionId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString("N");
}

public enum ArtifactKind
{
    Criteria = 1,
    Wireframe = 2,
    Code = 3,
    Diagram = 4,
    TestPlan = 5,
    Adr = 6,
    Doc = 7,
    /// <summary>Risk register / mitigation list, typically agent-generated.</summary>
    Risks = 8
}

public enum CanonicalStore
{
    HubNative = 1,
    Figma = 2,
    Git = 3,
    Miro = 4,
    Confluence = 5
}

public sealed record CanonicalPointer(CanonicalStore Store, string ExternalId, string? Url);

public enum AuthorKind
{
    Human = 1,
    Agent = 2,
    System = 3
}

/// <summary>
/// Who produced an artifact version. Humans carry a user id; agents carry the
/// run id that produced the version (so provenance walks straight back to
/// the assembled prompt + fragments). System authorship is reserved for
/// bootstrap / migration content.
/// </summary>
public sealed record Author(AuthorKind Kind, Guid Id);

/// <summary>
/// Pointer to bytes. Phase-4 only ever stores hub-native blobs (the URI
/// resolves against the application's blob store) but the shape supports
/// external URIs for when Figma/Git/Miro arrive — the canonical store still
/// owns the bytes; BlobRef is the addressable handle.
/// </summary>
public sealed record BlobRef(string Uri, string ContentType, long? SizeBytes);

/// <summary>
/// Soft lock on an artifact. Phase-4 uses time-based expiry rather than
/// keep-alives — a UI session refreshes by re-acquiring; if the holder
/// disconnects, the lock simply ages out. Project admins can release a
/// non-expired lock; that policy is enforced by the application service.
/// </summary>
public sealed record ArtifactLock(Guid HolderUserId, DateTimeOffset AcquiredAt, DateTimeOffset ExpiresAt)
{
    public bool IsActiveAt(DateTimeOffset now) => now < ExpiresAt;
}

/// <summary>
/// One immutable revision of an artifact's content. The chain is append-only;
/// a "rollback" is a new version that points to an older blob. Reason is a
/// short human-readable annotation ("PO accepted decompose"), not an audit
/// log replacement.
/// </summary>
public sealed class ArtifactVersion
{
    private ArtifactVersion() { } // EF Core

    private ArtifactVersion(
        ArtifactVersionId id,
        ArtifactId artifactId,
        int versionNumber,
        Author author,
        BlobRef content,
        BlobRef? preview,
        string? reason,
        DateTimeOffset createdAt)
    {
        Id = id;
        ArtifactId = artifactId;
        VersionNumber = versionNumber;
        Author = author;
        Content = content;
        Preview = preview;
        Reason = reason;
        CreatedAt = createdAt;
    }

    public ArtifactVersionId Id { get; private set; }
    public ArtifactId ArtifactId { get; private set; }
    public int VersionNumber { get; private set; }
    public Author Author { get; private set; } = null!;
    public BlobRef Content { get; private set; } = null!;
    public BlobRef? Preview { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    internal static ArtifactVersion Create(
        ArtifactId artifactId,
        int versionNumber,
        Author author,
        BlobRef content,
        BlobRef? preview,
        string? reason,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(author);
        ArgumentNullException.ThrowIfNull(content);
        if (versionNumber < 1)
        {
            throw new DomainException("Version number must be >= 1.");
        }
        return new ArtifactVersion(
            ArtifactVersionId.New(),
            artifactId,
            versionNumber,
            author,
            content,
            preview,
            string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            now);
    }
}

/// <summary>
/// An artifact attached to a FeatureNode. Carries an append-only version
/// chain and an optional soft lock. The aggregate enforces:
///
///   • PublishVersion is rejected while the artifact is locked by another
///     user (callers acquire the lock first, then publish, then release).
///   • Versions number monotonically from 1, never skipping.
///   • Lock expiry is time-based (no keep-alive); a stale lock is replaced
///     transparently when the next caller acquires.
/// </summary>
public sealed class Artifact
{
    private readonly List<ArtifactVersion> _versions = [];

    private Artifact() { } // EF Core

    private Artifact(
        ArtifactId id,
        NodeId nodeId,
        ArtifactKind kind,
        string title,
        CanonicalPointer canonical,
        DateTimeOffset createdAt)
    {
        Id = id;
        NodeId = nodeId;
        Kind = kind;
        Title = title;
        Canonical = canonical;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public ArtifactId Id { get; private set; }
    public NodeId NodeId { get; private set; }
    public ArtifactKind Kind { get; private set; }
    public string Title { get; private set; } = null!;
    public CanonicalPointer Canonical { get; private set; } = null!;
    public ArtifactLock? Lock { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? LastSyncedAt { get; private set; }

    public IReadOnlyList<ArtifactVersion> Versions => _versions.AsReadOnly();
    public ArtifactVersion? CurrentVersion => _versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();

    public static Artifact Create(
        NodeId nodeId,
        ArtifactKind kind,
        string title,
        CanonicalPointer canonical,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(canonical);
        return new Artifact(ArtifactId.New(), nodeId, kind, title.Trim(), canonical, now);
    }

    /// <summary>
    /// Append a new version. If the artifact is locked by a *different* user
    /// the call is rejected; the caller is expected to acquire the lock
    /// first. Versions number from 1 monotonically.
    /// </summary>
    public ArtifactVersion PublishVersion(
        Author author,
        BlobRef content,
        BlobRef? preview,
        string? reason,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(author);
        if (Lock is not null && Lock.IsActiveAt(now))
        {
            if (author.Kind == AuthorKind.Human && Lock.HolderUserId != author.Id)
            {
                throw new DomainException(
                    $"Artifact is locked by another user until {Lock.ExpiresAt:u}; acquire the lock before publishing.");
            }
            // Agents and System author publishes ignore human locks — agent
            // runs are queued by an authorised user and the lock is meant to
            // protect against concurrent human edits, not workflow output.
        }

        var next = (CurrentVersion?.VersionNumber ?? 0) + 1;
        var version = ArtifactVersion.Create(Id, next, author, content, preview, reason, now);
        _versions.Add(version);
        UpdatedAt = now;
        return version;
    }

    /// <summary>
    /// Acquire (or extend) the soft lock. If the existing lock is held by
    /// the same user we extend its expiry. If it's held by someone else but
    /// expired, we transparently replace it. An active lock by another user
    /// rejects.
    /// </summary>
    public void AcquireLock(Guid userId, TimeSpan duration, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("UserId is required to acquire a lock.");
        }
        if (duration <= TimeSpan.Zero)
        {
            throw new DomainException("Lock duration must be positive.");
        }
        if (Lock is not null && Lock.IsActiveAt(now) && Lock.HolderUserId != userId)
        {
            throw new DomainException(
                $"Artifact is already locked until {Lock.ExpiresAt:u} by another user.");
        }
        Lock = new ArtifactLock(userId, now, now + duration);
        UpdatedAt = now;
    }

    /// <summary>
    /// Release the lock. If the caller is not the holder and the lock is
    /// still active, the call is rejected — the application service is
    /// responsible for the "project admin can override" policy.
    /// </summary>
    public void ReleaseLock(Guid userId, DateTimeOffset now)
    {
        if (Lock is null)
        {
            return;
        }
        if (Lock.IsActiveAt(now) && Lock.HolderUserId != userId)
        {
            throw new DomainException("Only the lock holder may release an active lock.");
        }
        Lock = null;
        UpdatedAt = now;
    }

    public void ForceReleaseLock(DateTimeOffset now)
    {
        Lock = null;
        UpdatedAt = now;
    }

    public void MarkSynced(DateTimeOffset now) => LastSyncedAt = now;
}
