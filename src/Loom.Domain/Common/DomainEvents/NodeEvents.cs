using Loom.Domain.Nodes;

namespace Loom.Domain.Common.DomainEvents;

public sealed record NodeCreated(
    NodeId NodeId,
    Guid ProjectId,
    NodeId? ParentId,
    NodeType Type,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record NodeUpdated(
    NodeId NodeId,
    Guid ProjectId,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record NodePhaseAdvanced(
    NodeId NodeId,
    NodePhase From,
    NodePhase To,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record DiscoveryAccepted(
    NodeId NodeId,
    Guid ProjectId,
    Guid AcceptedBy,
    DateTimeOffset OccurredAt) : IDomainEvent;
