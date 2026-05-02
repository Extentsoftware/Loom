using Loom.Domain.Common;

namespace Loom.Domain.Fragments;

/// <summary>
/// A single immutable version of a fragment's content. Once a version is
/// recorded against a Run (i.e. used in production), it must never be edited
/// — provenance depends on it. To change a fragment, publish a new version.
/// </summary>
public sealed class FragmentVersion
{
    private FragmentVersion() { }

    private FragmentVersion(
        FragmentVersionId id,
        FragmentId fragmentId,
        int version,
        string content,
        EngineHints hints,
        string? changeNote,
        Guid authorId,
        DateTimeOffset createdAt)
    {
        Id = id;
        FragmentId = fragmentId;
        Version = version;
        Content = content;
        Hints = hints;
        ChangeNote = changeNote;
        AuthorId = authorId;
        CreatedAt = createdAt;
    }

    public FragmentVersionId Id { get; private set; }
    public FragmentId FragmentId { get; private set; }
    public int Version { get; private set; }
    public string Content { get; private set; } = null!;
    public EngineHints Hints { get; private set; } = null!;
    public string? ChangeNote { get; private set; }
    public Guid AuthorId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public bool IsDeprecated { get; private set; }

    internal static FragmentVersion Create(
        FragmentId fragmentId,
        int version,
        string content,
        EngineHints hints,
        string? changeNote,
        Guid authorId,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        if (version < 1)
        {
            throw new DomainException("Version must be 1 or greater.");
        }
        if (authorId == Guid.Empty)
        {
            throw new DomainException("Author id is required.");
        }
        return new FragmentVersion(
            FragmentVersionId.New(),
            fragmentId,
            version,
            content,
            hints,
            string.IsNullOrWhiteSpace(changeNote) ? null : changeNote.Trim(),
            authorId,
            now);
    }

    /// <summary>
    /// Mark this version as deprecated. Deprecated versions remain readable —
    /// existing Run records continue to reference them — but new runs will
    /// not be allowed to compose them in.
    /// </summary>
    public void Deprecate()
    {
        IsDeprecated = true;
    }
}
