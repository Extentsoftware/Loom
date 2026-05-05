using Loom.Domain.Nodes;
using Loom.Domain.Runs;
using Loom.Domain.Workflows;

namespace Loom.Application.Workflows;

/// <summary>
/// A projector turns a completed agent step's output (JSON or text) into
/// durable side-effects on the node — typically writing an Artifact, but
/// optionally updating other domain state. The engine resolves projectors
/// by <see cref="WorkflowStep.OutputSchemaName"/> after the step's Run is
/// marked complete, so a missing projector is silently a no-op (the
/// transcript still records the raw output).
///
/// Registration: scoped, multiple — the engine selects the first whose
/// <see cref="SchemaName"/> matches. This lets a workflow author add a
/// new step + projector pair without touching the engine.
/// </summary>
public interface IStepOutputProjector
{
    /// <summary>OutputSchemaName this projector handles (case-sensitive).</summary>
    string SchemaName { get; }

    Task ProjectAsync(NodeId nodeId, WorkflowStep workflowStep, RunId runId, string output, CancellationToken ct = default);
}
