using Loom.Domain.Common;
using Loom.Domain.Nodes;
using Loom.Domain.Runs;

namespace Loom.Application.Workflows.Kickoff;

/// <summary>
/// Orchestrates kickoff-acceptance flows that need to be atomic across
/// multiple aggregate writes. The PoGate Razor page used to inline the
/// accept-decompose loop, calling FeatureService once per child plus
/// SetIntentAsync separately, then ResolveGateAsync — that's 8+ saves
/// in two-state-per-child shape, with the outbox dispatcher firing
/// auto-queue handlers in between. This service collapses all of that
/// into a single transactional unit so partial failures don't leave the
/// PO with a half-built tree.
/// </summary>
public interface IKickoffService
{
    /// <summary>
    /// Accept the decompose proposal for <paramref name="parentNodeId"/>:
    /// create each accepted child (with intent set in the same save),
    /// emit a single <c>KickoffDecomposeAccepted</c> domain event, and
    /// resolve the decompose gate — all inside one transaction.
    /// Re-callable: if a partially-completed prior attempt left some
    /// children behind, the existing ones are returned as no-ops and
    /// the rest get filled in.
    /// </summary>
    /// <returns>The full set of child node ids under the parent after
    /// the accept (existing + newly-created).</returns>
    Task<IReadOnlyList<NodeId>> AcceptDecompositionAsync(
        RunId gateRunId,
        string gateStepKey,
        NodeId parentNodeId,
        IReadOnlyList<ProposedChildAcceptance> children,
        Guid acceptedBy,
        CancellationToken ct = default);
}

/// <summary>
/// One child the PO has accepted at the decompose gate. Intent is
/// optional — the agent may not have provided one for every proposal.
/// </summary>
public sealed record ProposedChildAcceptance(
    Slug Slug,
    NodeType Type,
    string Title,
    string? Intent,
    // Slug of another acceptance entry in the same batch that this one
    // nests under. null means top-level (under the kickoff parent node).
    // Resolution happens in KickoffService — children whose ParentSlug
    // does not match a sibling fall back to the kickoff parent.
    Slug? ParentSlug = null);
