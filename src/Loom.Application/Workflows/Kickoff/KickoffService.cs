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
            // Resolve nesting up front: each acceptance may name a
            // ParentSlug that points at another acceptance in this batch
            // (e.g. capabilities nesting under their feature). We sort
            // topologically — entries whose parent is also being created
            // here come after that parent — and keep a slug→NodeId map
            // so children land on the right NodeId. Anything whose
            // ParentSlug doesn't match a sibling falls back to the
            // kickoff parentNodeId.
            var ordered = TopologicalOrder(children);
            var slugToId = new Dictionary<string, NodeId>(StringComparer.Ordinal);
            var childIds = new List<NodeId>(ordered.Count);
            foreach (var c in ordered)
            {
                if (string.IsNullOrWhiteSpace(c.Title))
                {
                    continue;
                }
                var localParent = c.ParentSlug is { } ps && slugToId.TryGetValue(ps.Value, out var pid)
                    ? pid
                    : parentNodeId;
                // CreateChildNodeAsync is idempotent on slug — if a
                // sibling with this slug already exists the existing
                // one is returned. So a partial prior accept doesn't
                // block this one.
                var node = await features.CreateChildNodeAsync(
                    localParent, c.Slug, c.Type, c.Title.Trim(), acceptedBy,
                    intent: string.IsNullOrWhiteSpace(c.Intent) ? null : c.Intent.Trim(),
                    ct: ct);
                slugToId[c.Slug.Value] = node.Id;
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

    /// <summary>
    /// Order acceptances so that any entry whose ParentSlug matches
    /// another entry's Slug comes after that entry. Cycles are broken by
    /// emitting cycle members in their original order; bad pointers are
    /// tolerated and resolve to the kickoff parent at create-time.
    /// </summary>
    private static List<ProposedChildAcceptance> TopologicalOrder(
        IReadOnlyList<ProposedChildAcceptance> input)
    {
        var bySlug = new Dictionary<string, ProposedChildAcceptance>(StringComparer.Ordinal);
        foreach (var c in input)
        {
            ArgumentNullException.ThrowIfNull(c);
            bySlug[c.Slug.Value] = c;
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var onStack = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<ProposedChildAcceptance>(input.Count);

        void Visit(ProposedChildAcceptance c)
        {
            if (!visited.Add(c.Slug.Value))
            {
                return;
            }
            onStack.Add(c.Slug.Value);
            if (c.ParentSlug is { } ps
                && bySlug.TryGetValue(ps.Value, out var parent)
                && !onStack.Contains(parent.Slug.Value))
            {
                Visit(parent);
            }
            onStack.Remove(c.Slug.Value);
            ordered.Add(c);
        }

        foreach (var c in input)
        {
            Visit(c);
        }
        return ordered;
    }
}
