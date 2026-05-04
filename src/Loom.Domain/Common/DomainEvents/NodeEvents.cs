using Loom.Domain.Nodes;

namespace Loom.Domain.Common.DomainEvents;

public sealed record NodeCreated(
    NodeId NodeId,
    Guid ProjectId,
    NodeId? ParentId,
    NodeType Type,
    DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>
/// What changed on the node. The Outbox persists the event with its
/// payload, which gives us a queryable audit trail per kind without
/// needing a separate audit table — handlers that don't care just
/// ignore the field, and the audit query is a payload-shape filter
/// on the outbox.
/// </summary>
public enum NodeEditKind
{
    Other = 0,
    Title = 1,
    Intent = 2,
    Outcomes = 3,
    Hypotheses = 4,
    Constraints = 5,
    OpenQuestions = 6,
    Stakeholders = 7
}

public sealed record NodeUpdated(
    NodeId NodeId,
    Guid ProjectId,
    DateTimeOffset OccurredAt,
    NodeEditKind Kind = NodeEditKind.Other,
    Guid Actor = default) : IDomainEvent;

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
