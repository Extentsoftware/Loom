using Loom.Domain.Common;

namespace Loom.Domain.Fragments;

public readonly record struct FragmentId(Guid Value) : IEntityId
{
    public static FragmentId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString("N");
}

public readonly record struct FragmentVersionId(Guid Value) : IEntityId
{
    public static FragmentVersionId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString("N");
}
