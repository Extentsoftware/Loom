using Loom.Application.Abstractions;
using Loom.Application.Features;
using Loom.Domain.Common.DomainEvents;
using Loom.Domain.Nodes;
using Loom.Domain.Runs;

namespace Loom.Application.Workflows.Kickoff;

public sealed class KickoffService(
    IFeatureService features,
    IWorkflowEngine engine,
    IDomainEventCollector events,
    ISystemClock clock,
    IUnitOfWork uow) : IKickoffService
{
    public async Task<IReadOnlyList<NodeId>> AcceptDecompositionAsync(
        RunId gateRunId,
        NodeId parentNodeId,
        IReadOnlyList<ProposedChildAcceptance> children,
        Guid acceptedBy,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(children);

        await using var tx = await uow.BeginTransactionAsync(ct);
        try
        {
            var childIds = new List<NodeId>(children.Count);
            foreach (var c in children)
            {
                ArgumentNullException.ThrowIfNull(c);
                if (string.IsNullOrWhiteSpace(c.Title))
                {
                    continue;
                }
                // CreateChildNodeAsync is idempotent on slug — if a
                // sibling with this slug already exists the existing
                // one is returned. So a partial prior accept doesn't
                // block this one.
                var node = await features.CreateChildNodeAsync(
                    parentNodeId, c.Slug, c.Type, c.Title.Trim(), acceptedBy,
                    intent: string.IsNullOrWhiteSpace(c.Intent) ? null : c.Intent.Trim(),
                    ct: ct);
                childIds.Add(node.Id);
            }

            // Single post-commit signal that the kickoff decomposition
            // is fully accepted. EnrichmentAutoQueueHandler subscribes
            // to this rather than firing per-NodeCreated, so enrichment
            // runs only start after the user's transaction commits.
            events.Add(new KickoffDecomposeAccepted(
                parentNodeId, childIds, acceptedBy, clock.UtcNow));

            // Resolve the gate inside the transaction so the run-state
            // change and the accepted children commit atomically.
            // ResolveGateAsync for the kickoff `decompose` step has no
            // further agent steps to fire (decompose is the last step
            // in the kickoff workflow), so this stays short-running.
            await engine.ResolveGateAsync(
                gateRunId, KickoffWorkflowFactory.DecomposeStepKey, acceptedBy, edits: null, ct);

            await tx.CommitAsync(ct);
            return childIds;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
