using Loom.Application.Abstractions;
using Loom.Domain.Common;
using Loom.Domain.Common.DomainEvents;

namespace Loom.Application.Workflows.Enrichment;

/// <summary>
/// Outbox handler that auto-queues an enrichment workflow execution for
/// each child node accepted at the kickoff decompose gate. Subscribes to
/// <see cref="KickoffDecomposeAccepted"/> rather than the per-child
/// <c>NodeCreated</c> event — that earlier shape fired in the middle of
/// the user's accept-decompose loop and contended for row locks with
/// the user's pending writes. Listening to a single post-commit signal
/// guarantees enrichment only kicks in after the PO's transaction is
/// fully committed.
///
/// Handler is best-effort: if the enrichment workflow doesn't yet
/// exist (bootstrapper races at first run) it logs and skips. The
/// manual /enrich button will work once it does.
/// </summary>
public sealed class EnrichmentAutoQueueHandler(
    IWorkflowEngine engine,
    IWorkflowRepository workflows) : IDomainEventHandler<KickoffDecomposeAccepted>
{
    public async Task HandleAsync(KickoffDecomposeAccepted evt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evt);
        if (evt.ChildNodeIds.Count == 0)
        {
            return;
        }

        var workflow = await workflows.GetCurrentByKeyAsync(
            Slug.From(EnrichmentWorkflowFactory.WorkflowKey), ct);
        if (workflow is null)
        {
            // Bootstrapper hasn't seeded yet; nothing to do.
            return;
        }

        // Queue enrichment per child. Each StartAsync creates its own
        // Run records; failures on one child don't block the others
        // (any per-child failure shows on the Agent Activity page).
        foreach (var childId in evt.ChildNodeIds)
        {
            try
            {
                await engine.StartAsync(
                    childId, workflow.Id, new Dictionary<string, string>(), ct);
            }
            catch
            {
                // Best-effort — enrichment is idempotent at the
                // workflow level (re-runnable from the workspace),
                // so a failure here just means the user kicks it
                // off manually.
            }
        }
    }
}
