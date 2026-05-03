using Loom.Application.Abstractions;
using Loom.Domain.Common;
using Loom.Domain.Fragments;
using Loom.Domain.Nodes;
using Loom.Domain.Workflows;

namespace Loom.Application.Fragments;

public sealed class FragmentService(
    IFragmentRepository fragments,
    IFeatureNodeRepository nodes,
    IUnitOfWork uow,
    ISystemClock clock) : IFragmentService
{
    public async Task<Fragment> CreateAsync(
        Slug key,
        FragmentCategory category,
        FragmentScope scope,
        Guid? scopeId,
        string title,
        Guid ownerId,
        CancellationToken ct = default)
    {
        var existing = await fragments.GetByKeyAsync(key.Value, scope, scopeId, ct);
        if (existing is not null)
        {
            throw new DomainException(
                $"A {scope} fragment with key '{key.Value}' already exists in this scope.");
        }

        var f = Fragment.Create(key, category, scope, scopeId, title, ownerId, clock.UtcNow);
        await fragments.AddAsync(f, ct);
        await uow.SaveChangesAsync(ct);
        return f;
    }

    public async Task<FragmentVersion> PublishVersionAsync(
        FragmentId fragmentId,
        string content,
        EngineHints hints,
        string? changeNote,
        Guid authorId,
        CancellationToken ct = default)
    {
        var f = await fragments.GetAsync(fragmentId, ct)
            ?? throw new DomainException($"Fragment {fragmentId} not found.");
        var v = f.PublishVersion(content, hints, changeNote, authorId, clock.UtcNow);
        await uow.SaveChangesAsync(ct);
        return v;
    }

    public async Task<IReadOnlyList<EffectiveFragment>> GetEffectiveFragmentsAsync(
        NodeId nodeId,
        IReadOnlyList<FragmentSelector> selectors,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(selectors);
        if (selectors.Count == 0)
        {
            return [];
        }

        var node = await nodes.GetAsync(nodeId, ct)
            ?? throw new DomainException($"Node {nodeId} not found.");

        // Build ancestor chain from project root to node (exclusive of self).
        var ancestorIds = new List<NodeId>();
        var cursorId = node.ParentId;
        while (cursorId.HasValue)
        {
            ancestorIds.Add(cursorId.Value);
            var parent = await nodes.GetAsync(cursorId.Value, ct);
            cursorId = parent?.ParentId;
        }
        // Closest ancestor first.
        ancestorIds.Reverse();

        // Resolve scope buckets.
        var globalSet = await fragments.ListGlobalAsync(ct);
        var projectSet = await fragments.ListByProjectAsync(node.ProjectId, ct);
        var ancestorSets = new List<(NodeId AncestorId, IReadOnlyList<Fragment> Fragments)>();
        foreach (var ancestorId in ancestorIds)
        {
            ancestorSets.Add((ancestorId, await fragments.ListByNodeAsync(ancestorId.Value, ct)));
        }
        var nodeSet = await fragments.ListByNodeAsync(nodeId.Value, ct);

        var result = new Dictionary<(FragmentCategory, string), EffectiveFragment>();

        // Order applies "broadest first, narrowest last" so later writes
        // overwrite earlier ones — which is the inheritance rule (local
        // overrides win).
        foreach (var sel in selectors)
        {
            TryAdd(result, sel, globalSet, EffectiveFragmentSource.Global);
            TryAdd(result, sel, projectSet, EffectiveFragmentSource.Project);
            foreach (var (_, ancestorFragments) in ancestorSets)
            {
                TryAdd(result, sel, ancestorFragments, EffectiveFragmentSource.Ancestor);
            }
            TryAdd(result, sel, nodeSet, EffectiveFragmentSource.Node);
        }

        return result.Values
            .OrderBy(e => (int)e.Source)
            .ThenBy(e => e.Fragment.Category)
            .ThenBy(e => e.Fragment.Key.Value, StringComparer.Ordinal)
            .ToList();
    }

    private static void TryAdd(
        Dictionary<(FragmentCategory, string), EffectiveFragment> bag,
        FragmentSelector selector,
        IReadOnlyList<Fragment> candidates,
        EffectiveFragmentSource source)
    {
        var match = candidates.FirstOrDefault(f =>
            f.Category == selector.Category && f.Key == selector.Key);
        if (match is null)
        {
            return;
        }
        var current = match.CurrentVersion;
        if (current is null || current.IsDeprecated)
        {
            return;
        }
        bag[(selector.Category, selector.Key.Value)] = new EffectiveFragment(match, current, source);
    }
}
