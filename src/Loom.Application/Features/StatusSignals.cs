using Loom.Domain.Nodes;
using Loom.Domain.Runs;

namespace Loom.Application.Features;

/// <summary>
/// Derived health/activity indicator for a node. Computed from phase, child
/// phases, and recent run states — never set directly. The Operating Picture
/// renders this as a colored pulse beside each tree row.
/// </summary>
public sealed record StatusSignals(
    StatusPulse Pulse,
    int OpenChildren,
    int RunningRuns,
    int FailedRuns,
    DateTimeOffset? LastActivityAt);

public enum StatusPulse
{
    /// <summary>Nothing in flight; node and children at rest.</summary>
    Idle = 1,
    /// <summary>Active work in progress: runs running or recently moved.</summary>
    Active = 2,
    /// <summary>Waiting on a human gate.</summary>
    Awaiting = 3,
    /// <summary>Recent failure or cancelled run; needs attention.</summary>
    Attention = 4,
    /// <summary>Terminal — Done or Archived.</summary>
    Done = 5
}

internal static class StatusSignalsCalculator
{
    public static StatusSignals Compute(
        FeatureNode node,
        IReadOnlyList<FeatureNode> children,
        IReadOnlyList<Run> recentRuns,
        DateTimeOffset now)
    {
        var openChildren = children.Count(c => c.Phase is not (NodePhase.Done or NodePhase.Archived));
        var running = recentRuns.Count(r => r.State is RunState.Running);
        var awaiting = recentRuns.Count(r => r.State is RunState.PausedForHuman);
        var failed = recentRuns.Count(r => r.State is RunState.Failed);

        var lastActivity = recentRuns
            .Select(r => r.CompletedAt ?? r.StartedAt ?? r.CreatedAt)
            .DefaultIfEmpty(node.UpdatedAt)
            .Max();

        var pulse = node.Phase switch
        {
            NodePhase.Done or NodePhase.Archived => StatusPulse.Done,
            _ when failed > 0 => StatusPulse.Attention,
            _ when awaiting > 0 => StatusPulse.Awaiting,
            _ when running > 0 => StatusPulse.Active,
            _ => StatusPulse.Idle
        };

        return new StatusSignals(pulse, openChildren, running, failed, lastActivity);
    }
}
