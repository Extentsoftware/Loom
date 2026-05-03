using Loom.Domain.Common;
using Loom.Domain.Nodes;

namespace Loom.Domain.Artifacts;

public readonly record struct ArtifactId(Guid Value) : IEntityId
{
    public static ArtifactId New() => new(Guid.CreateVersion7());
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
    Doc = 7
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

/// <summary>
/// Descriptor for a piece of canonical content attached to a FeatureNode.
/// Aggregate behaviour (creation, locking, sync) is intentionally not yet
/// modelled — this type exists to satisfy the persistence layer; construction
/// rules belong in a follow-up that designs the Artifact aggregate properly.
/// </summary>
public sealed class Artifact
{
    private Artifact() { } // EF Core

    public ArtifactId Id { get; private set; }
    public NodeId NodeId { get; private set; }
    public ArtifactKind Kind { get; private set; }
    public string Title { get; private set; } = null!;
    public CanonicalPointer Canonical { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? LastSyncedAt { get; private set; }
}
