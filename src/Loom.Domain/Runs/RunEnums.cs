using Loom.Domain.Common;

namespace Loom.Domain.Runs;

public readonly record struct RunId(Guid Value) : IEntityId
{
    public static RunId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString("N");
}

/// <summary>
/// State machine for a Run. Allowed transitions:
///   Queued → Running
///   Running → PausedForHuman | Completed | Failed | Cancelled
///   PausedForHuman → Running | Cancelled
/// Terminal states (Completed, Failed, Cancelled) are immutable.
/// </summary>
public enum RunState
{
    Queued = 1,
    Running = 2,
    PausedForHuman = 3,
    Completed = 4,
    Failed = 5,
    Cancelled = 6
}

/// <summary>
/// Engine that executes a run. Loom abstracts over many engines via IAgentRuntime;
/// this enum identifies which one. Foundry is the v1 default; Anthropic / Claude
/// Code Headless / in-proc come later.
/// </summary>
public enum EngineName
{
    Foundry = 1,
    Anthropic = 2,
    ClaudeCodeHeadless = 3,
    InProc = 4
}

/// <summary>
/// Kinds of audit events recorded against a Run. The set is append-only
/// (existing values must keep their numbers); add new kinds at the end.
/// </summary>
public enum RunEventKind
{
    StepStarted = 1,
    StepOutput = 2,
    StepFailed = 3,
    GatePaused = 4,
    GateResolved = 5,
    StepCompleted = 6,
    TokenUsage = 7,
    /// <summary>Phase 3+: a notification was emitted for this run.</summary>
    NotificationSent = 8,
    /// <summary>Phase 3+: a replay of a prior run was queued from this one.</summary>
    ReplayQueued = 9
}
