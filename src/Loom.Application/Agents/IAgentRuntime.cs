using Loom.Domain.Runs;

namespace Loom.Application.Agents;

/// <summary>
/// Abstraction over a backend that executes agent runs (Anthropic, Foundry,
/// Claude Code Headless, in-proc). Each implementation lives in its own
/// project under Loom.Agents.* per the architecture decision; the router
/// picks one per step based on declared engine_pref + capacity + budgets.
/// </summary>
public interface IAgentRuntime
{
    EngineName Engine { get; }
    EngineCapabilities Capabilities { get; }

    /// <summary>
    /// Begin a run. Returns immediately with the engine's external run id;
    /// the caller subscribes via StreamEventsAsync to receive progress.
    /// </summary>
    Task<string> StartAsync(AgentRunRequest request, CancellationToken ct = default);

    /// <summary>
    /// Cancel a run by external id. Idempotent — already-terminal runs are
    /// no-ops.
    /// </summary>
    Task CancelAsync(string externalRunId, CancellationToken ct = default);

    /// <summary>
    /// Stream of events for a started run. Disposes when the run reaches a
    /// terminal state (Completed | Failed | Cancelled). Implementations may
    /// replay history if reconnected after a hot restart.
    /// </summary>
    IAsyncEnumerable<AgentRunEvent> StreamEventsAsync(string externalRunId, CancellationToken ct = default);
}
