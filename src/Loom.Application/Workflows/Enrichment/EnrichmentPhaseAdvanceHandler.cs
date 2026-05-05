using Loom.Application.Abstractions;
using Loom.Domain.Common;
using Loom.Domain.Common.DomainEvents;
using Loom.Domain.Nodes;

namespace Loom.Application.Workflows.Enrichment;

/// <summary>
/// Outbox handler that queues an enrichment workflow run when a node is
/// advanced into the <see cref="NodePhase.Enrich"/> phase from the
/// workspace's "Advance to enrich" button. Mirrors the post-commit shape
/// of <see cref="EnrichmentAutoQueueHandler"/>, but listens for the
/// per-node phase change rather than the kickoff-decompose batch event.
///
/// Idempotent: if the node already has a non-terminal run against the
/// current enrichment workflow we skip — the workspace's existing run
/// chip stays the source of truth.
/// </summary>
public sealed class EnrichmentPhaseAdvanceHandler(
    IWorkflowEngine engine,
    IWorkflowRepository workflows,
    IRunRepository runs) : IDomainEventHandler<NodePhaseAdvanced>
{
    public async Task HandleAsync(NodePhaseAdvanced evt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evt);
        if (evt.To != NodePhase.Enrich)
        {
            return;
        }

        var workflow = await workflows.GetCurrentByKeyAsync(
            Slug.From(EnrichmentWorkflowFactory.WorkflowKey), ct);
        if (workflow is null)
        {
            return;
        }

        // Skip if there's already an active enrichment run for this node
        // against the current workflow version. The workspace re-runs
        // path stays manual via the /enrich button.
        var existing = await runs.GetByNodeAsync(evt.NodeId, ct);
        if (existing.Any(r => r.WorkflowId == workflow.Id.Value && !r.IsTerminal))
        {
            return;
        }

        try
        {
            await engine.StartAsync(
                evt.NodeId, workflow.Id, new Dictionary<string, string>(), ct);
        }
        catch
        {
            // Best-effort — manual /enrich button remains the fallback.
        }
    }
}
