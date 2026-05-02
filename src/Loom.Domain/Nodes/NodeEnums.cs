namespace Loom.Domain.Nodes;

/// <summary>
/// The granularity of a feature node. The same shape applies at every level;
/// the type is metadata that drives UI affordances and decomposition rules.
/// </summary>
public enum NodeType
{
    Initiative = 1,
    Feature = 2,
    Capability = 3,
    Slice = 4
}

/// <summary>
/// Lifecycle phase of a node. Phase is a *declared* value (set by the workflow
/// engine and human gates); status — what is actually happening right now — is
/// derived from runs and child phases and lives outside the entity.
/// </summary>
public enum NodePhase
{
    Discovery = 1,
    Enrich = 2,
    Build = 3,
    Test = 4,
    Done = 5,
    Archived = 6
}
