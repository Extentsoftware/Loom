using Loom.Domain.Nodes;
using Loom.Domain.Runs;

namespace Loom.Domain.Common.DomainEvents;

public sealed record RunQueued(
    RunId RunId,
    NodeId NodeId,
    Guid? WorkflowId,
    Guid? StepId,
    EngineName Engine,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record RunStarted(
    RunId RunId,
    string ExternalRunId,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record RunPausedForHuman(
    RunId RunId,
    string StepKey,
    WorkflowStepGatingRole GatingRole,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record RunCompleted(
    RunId RunId,
    decimal CostUsd,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record RunFailed(
    RunId RunId,
    string Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record RunCancelled(
    RunId RunId,
    string Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>
/// Wire-stable role designator for the human gate. Mirrors WorkflowStepGating
/// but excludes Auto — auto steps never produce a RunPausedForHuman event.
/// Kept narrow so the event payload doesn't drift if WorkflowStepGating gains
/// additional auto-only kinds.
/// </summary>
public enum WorkflowStepGatingRole
{
    Po = 1,
    Ux = 2,
    Lead = 3
}
