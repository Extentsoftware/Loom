using Loom.Domain.Common;

namespace Loom.Domain.Fragments;

/// <summary>
/// A tagged, versioned piece of prompt content. The Fragment aggregate manages
/// the version stream; FragmentVersion is an immutable child entity within it.
///
/// Identity = (Category, Key) within a Scope. The Key is a short slug used in
/// references like "methodology:problem-framing" — by convention the category
/// prefix matches the Category field, but it is not enforced (legacy keys may
/// drift; we'll add validation in a future ADR).
/// </summary>
public sealed class Fragment
{
    private readonly List<FragmentVersion> _versions = [];
    private readonly List<string> _tags = [];

    private Fragment() { }

    private Fragment(
        FragmentId id,
        Slug key,
        FragmentCategory category,
        FragmentScope scope,
        Guid? scopeId,
        string title,
        Guid ownerId,
        DateTimeOffset createdAt)
    {
        Id = id;
        Key = key;
        Category = category;
        Scope = scope;
        ScopeId = scopeId;
        Title = title;
        OwnerId = ownerId;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public FragmentId Id { get; private set; }
    public Slug Key { get; private set; }
    public FragmentCategory Category { get; private set; }
    public FragmentScope Scope { get; private set; }

    /// <summary>
    /// For Project-scoped fragments, the project id; for Node-scoped, the node id.
    /// Null for Global-scoped fragments.
    /// </summary>
    public Guid? ScopeId { get; private set; }

    public string Title { get; private set; } = null!;
    public Guid OwnerId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public FragmentVersionId? CurrentVersionId { get; private set; }

    public IReadOnlyList<string> Tags => _tags.AsReadOnly();
    public IReadOnlyList<FragmentVersion> Versions => _versions.AsReadOnly();

    public static Fragment Create(
        Slug key,
        FragmentCategory category,
        FragmentScope scope,
        Guid? scopeId,
        string title,
        Guid ownerId,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (ownerId == Guid.Empty)
        {
            throw new DomainException("Owner id is required.");
        }

        // Scope/ScopeId consistency: Global has no scope id; Project and Node require one.
        if (scope == FragmentScope.Global && scopeId.HasValue)
        {
            throw new DomainException("Global-scoped fragments must not carry a scope id.");
        }
        if (scope is FragmentScope.Project or FragmentScope.Node && (!scopeId.HasValue || scopeId == Guid.Empty))
        {
            throw new DomainException($"{scope}-scoped fragments require a scope id.");
        }

        return new Fragment(FragmentId.New(), key, category, scope, scopeId, title.Trim(), ownerId, now);
    }

    /// <summary>
    /// Publish a new version of this fragment. Becomes the current version.
    /// Previous versions remain readable for provenance but are no longer
    /// composed into new runs by default.
    /// </summary>
    public FragmentVersion PublishVersion(
        string content,
        EngineHints hints,
        string? changeNote,
        Guid authorId,
        DateTimeOffset now)
    {
        var nextNumber = _versions.Count == 0 ? 1 : _versions.Max(v => v.Version) + 1;
        var version = FragmentVersion.Create(Id, nextNumber, content, hints, changeNote, authorId, now);
        _versions.Add(version);
        CurrentVersionId = version.Id;
        UpdatedAt = now;
        return version;
    }

    public void AddTag(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        var normalized = tag.Trim().ToLowerInvariant();
        if (!_tags.Contains(normalized))
        {
            _tags.Add(normalized);
        }
    }

    public void RemoveTag(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        _tags.Remove(tag.Trim().ToLowerInvariant());
    }

    public void Rename(string title, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title = title.Trim();
        UpdatedAt = now;
    }

    public FragmentVersion? CurrentVersion =>
        CurrentVersionId.HasValue ? _versions.FirstOrDefault(v => v.Id == CurrentVersionId.Value) : null;
}
