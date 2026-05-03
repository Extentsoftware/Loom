using Loom.Domain.Common;

namespace Loom.Domain.Workflows;

public readonly record struct WorkflowId(Guid Value) : IEntityId
{
    public static WorkflowId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString("N");
}

public readonly record struct WorkflowStepId(Guid Value) : IEntityId
{
    public static WorkflowStepId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString("N");
}

public readonly record struct WorkflowExecutionId(Guid Value) : IEntityId
{
    public static WorkflowExecutionId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString("N");
}
