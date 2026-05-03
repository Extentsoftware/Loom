using Loom.Domain.Common;
using Loom.Domain.Fragments;
using Loom.Domain.Nodes;
using Loom.Domain.Workflows;

namespace Loom.Application.Fragments;

public interface IFragmentService
{
    Task<Fragment> CreateAsync(
        Slug key,
        FragmentCategory category,
        FragmentScope scope,
        Guid? scopeId,
        string title,
        Guid ownerId,
        CancellationToken ct = default);

    Task<FragmentVersion> PublishVersionAsync(
        FragmentId fragmentId,
        string content,
        EngineHints hints,
        string? changeNote,
        Guid authorId,
        CancellationToken ct = default);

    /// <summary>
    /// Resolves the effective fragment set for a given node and step.
    /// Order: global fragments matching selectors, then project, then ancestor
    /// chain (closest first), then node. A more-specific scope's match wins
    /// over a less-specific one for the same selector.
    /// </summary>
    Task<IReadOnlyList<EffectiveFragment>> GetEffectiveFragmentsAsync(
        NodeId nodeId,
        IReadOnlyList<FragmentSelector> selectors,
        CancellationToken ct = default);
}
