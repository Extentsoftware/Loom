using Loom.Domain.Common;

namespace Loom.Domain.Nodes;

public readonly record struct NodeId(Guid Value) : IEntityId
{
    public static NodeId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString("N");
}
