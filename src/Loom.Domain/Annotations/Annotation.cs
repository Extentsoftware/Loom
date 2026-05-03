using Loom.Domain.Common;

namespace Loom.Domain.Annotations;

public readonly record struct AnnotationId(Guid Value) : IEntityId
{
    public static AnnotationId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString("N");
}

/// <summary>
/// Phase-3 ships the schema only — the UI for annotating artifacts arrives
/// in Phase 4 with the Artifact Inspector. The shape is forward-compatible:
/// the Target value object is polymorphic so a future annotation can anchor
/// to a Figma frame, a code line range, a table cell, etc.
/// </summary>
public sealed class Annotation
{
    private readonly List<string> _tags = [];

    private Annotation() { } // EF Core

    private Annotation(
        AnnotationId id,
        Guid artifactId,
        Guid? userId,
        AnnotationKind kind,
        AnnotationTarget? target,
        string body,
        DateTimeOffset createdAt)
    {
        Id = id;
        ArtifactId = artifactId;
        UserId = userId;
        Kind = kind;
        Target = target;
        Body = body;
        CreatedAt = createdAt;
    }

    public AnnotationId Id { get; private set; }
    public Guid ArtifactId { get; private set; }
    public Guid? UserId { get; private set; }
    public AnnotationKind Kind { get; private set; }
    public AnnotationTarget? Target { get; private set; }
    public string Body { get; private set; } = null!;
    public IReadOnlyList<string> Tags => _tags.AsReadOnly();
    public DateTimeOffset CreatedAt { get; private set; }

    public static Annotation Create(
        Guid artifactId,
        Guid? userId,
        AnnotationKind kind,
        AnnotationTarget? target,
        string body,
        IEnumerable<string>? tags,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        var ann = new Annotation(AnnotationId.New(), artifactId, userId, kind, target, body.Trim(), now);
        if (tags is not null)
        {
            foreach (var t in tags)
            {
                if (string.IsNullOrWhiteSpace(t))
                {
                    continue;
                }
                var normalized = t.Trim().ToLowerInvariant();
                if (!ann._tags.Contains(normalized))
                {
                    ann._tags.Add(normalized);
                }
            }
        }
        return ann;
    }
}

public enum AnnotationKind
{
    Comment = 1,
    Critique = 2,
    Approval = 3,
    Concern = 4
}

/// <summary>
/// Polymorphic anchor for an annotation. <c>Locator</c> is system-specific
/// JSON (Figma frame id + bbox; code file + line range; etc.); <c>Kind</c>
/// names the system. Phase-4 + onward decode this.
/// </summary>
public sealed record AnnotationTarget(string Kind, string Locator);
