using Loom.Domain.Nodes;
using Loom.Domain.Runs;

namespace Loom.Application.Runs;

public interface IRunService
{
    Task<Run> QueueAsync(
        NodeId nodeId,
        Guid? workflowId,
        Guid? stepId,
        EngineName engine,
        Budgets budgets,
        IReadOnlyList<FragmentRef> fragments,
        CancellationToken ct = default);

    Task AttachAssembledPromptAsync(RunId runId, AssembledPrompt prompt, CancellationToken ct = default);

    Task MarkRunningAsync(RunId runId, string externalRunId, CancellationToken ct = default);

    Task RecordEventAsync(RunId runId, RunEvent runEvent, CancellationToken ct = default);

    Task<RunEvent> AppendEventAsync(
        RunId runId,
        Func<int, RunEvent> factory,
        CancellationToken ct = default);

    Task CompleteAsync(RunId runId, Cost cost, CancellationToken ct = default);

    Task FailAsync(RunId runId, string reason, Cost? partialCost, CancellationToken ct = default);

    Task PauseForHumanAsync(RunId runId, CancellationToken ct = default);

    Task ResumeAsync(RunId runId, CancellationToken ct = default);

    Task CancelAsync(RunId runId, string reason, CancellationToken ct = default);

    /// <summary>
    /// Queue a fresh run that mirrors the source run's setup. Phase-3
    /// implementation pins the *same* fragment versions the source used
    /// (deterministic replay); a Phase-7 variant will let the caller opt
    /// into "latest versions" via a flag. The source run is annotated
    /// with a ReplayQueued event so Run Detail surfaces the link.
    /// </summary>
    Task<Run> ReplayAsync(RunId sourceRunId, Guid requestedBy, CancellationToken ct = default);
}
