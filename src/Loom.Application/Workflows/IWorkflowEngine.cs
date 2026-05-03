using Loom.Domain.Nodes;
using Loom.Domain.Runs;
using Loom.Domain.Workflows;

namespace Loom.Application.Workflows;

public interface IWorkflowEngine
{
    /// <summary>
    /// Begin executing a workflow against a node. Walks steps in order: each
    /// agent step composes a prompt + queues a Run + dispatches via the agent
    /// router; each in-proc step invokes the registered IInProcStep; each
    /// human-gate step pauses and awaits ResolveGateAsync. Initial inputs are
    /// step-keyed (e.g. {"normalize.transcript": "..."}).
    /// </summary>
    Task<RunId?> StartAsync(
        NodeId nodeId,
        WorkflowId workflowId,
        IReadOnlyDictionary<string, string> initialInputs,
        CancellationToken ct = default);

    /// <summary>
    /// Resolve a paused human gate, optionally with edits to the agent's
    /// output. The engine then either advances to the next step or completes
    /// the workflow if no more steps remain.
    /// </summary>
    Task ResolveGateAsync(
        RunId runId,
        string stepKey,
        Guid resolvedBy,
        IReadOnlyDictionary<string, string>? edits = null,
        CancellationToken ct = default);
}
