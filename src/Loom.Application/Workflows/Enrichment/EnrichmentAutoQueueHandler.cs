using Loom.Application.Abstractions;
using Loom.Domain.Common;
using Loom.Domain.Common.DomainEvents;

namespace Loom.Application.Workflows.Enrichment;

/// <summary>
/// Outbox handler that auto-queues an enrichment workflow execution for
/// every newly-created child node. The PO accepts the decompose proposal
/// → each accepted child creates a NodeCreated event → this handler picks
/// it up off the outbox and starts the enrichment workflow against the
/// child. The handler is best-effort: if the workflow doesn't exist yet,
/// it logs and skips (the bootstrapper races at first run).
///
/// Root nodes (those without a parent) are explicitly skipped because the
/// kickoff workflow is the producer for those.
/// </summary>
public sealed class EnrichmentAutoQueueHandler(
    IWorkflowEngine engine,
    IWorkflowRepository workflows) : IDomainEventHandler<NodeCreated>
{
    public async Task HandleAsync(NodeCreated evt, CancellationToken ct = default)
    {
        if (evt.ParentId is null)
        {
            // Root nodes are kickoff targets; their lifecycle is the
            // kickoff workflow, not enrichment.
            return;
        }

        var workflow = await workflows.GetCurrentByKeyAsync(
            Slug.From(EnrichmentWorkflowFactory.WorkflowKey), ct);
        if (workflow is null)
        {
            // Bootstrapper hasn't seeded yet, or seeding was disabled.
            // Skip — manual /enrich button will work once it does.
            return;
        }

        await engine.StartAsync(
            evt.NodeId,
            workflow.Id,
            new Dictionary<string, string>(),
            ct);
    }
}
