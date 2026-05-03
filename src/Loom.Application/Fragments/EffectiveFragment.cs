using Loom.Domain.Fragments;

namespace Loom.Application.Fragments;

/// <summary>
/// A fragment resolved into the effective set for a node + workflow step.
/// Source records the scope at which the fragment was found, which the UI
/// surfaces for "where does this prompt come from" queries.
/// </summary>
public sealed record EffectiveFragment(
    Fragment Fragment,
    FragmentVersion Version,
    EffectiveFragmentSource Source);

public enum EffectiveFragmentSource
{
    Global = 1,
    Project = 2,
    Ancestor = 3,
    Node = 4
}
