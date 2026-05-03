using Loom.Domain.Common;
using Loom.Domain.Runs;

namespace Loom.Domain.Workflows;

/// <summary>
/// One step in a Workflow DAG. Steps are sequential within a workflow version
/// (Order field); branching workflows are not modelled in Phase 1. Identity is
/// (WorkflowId, Key); the Key is the human-readable token used in YAML and in
/// gate URLs ("normalize", "discovery", "decompose").
/// </summary>
public sealed class WorkflowStep
{
    private readonly List<FragmentSelector> _fragmentSelectors = [];

    private WorkflowStep() { } // EF Core

    private WorkflowStep(
        WorkflowStepId id,
        WorkflowId workflowId,
        int order,
        string key,
        WorkflowStepKind kind,
        WorkflowStepGating gating,
        EngineName? enginePref,
        string? outputSchemaName,
        Budgets budgets)
    {
        Id = id;
        WorkflowId = workflowId;
        Order = order;
        Key = key;
        Kind = kind;
        Gating = gating;
        EnginePref = enginePref;
        OutputSchemaName = outputSchemaName;
        Budgets = budgets;
    }

    public WorkflowStepId Id { get; private set; }
    public WorkflowId WorkflowId { get; private set; }
    public int Order { get; private set; }
    public string Key { get; private set; } = null!;
    public WorkflowStepKind Kind { get; private set; }
    public WorkflowStepGating Gating { get; private set; }
    public EngineName? EnginePref { get; private set; }
    public string? OutputSchemaName { get; private set; }
    public Budgets Budgets { get; private set; } = null!;

    public IReadOnlyList<FragmentSelector> FragmentSelectors => _fragmentSelectors.AsReadOnly();

    internal static WorkflowStep Create(
        WorkflowId workflowId,
        int order,
        string key,
        WorkflowStepKind kind,
        WorkflowStepGating gating,
        EngineName? enginePref,
        string? outputSchemaName,
        Budgets budgets,
        IEnumerable<FragmentSelector> selectors)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(budgets);
        ArgumentNullException.ThrowIfNull(selectors);
        if (order < 0)
        {
            throw new DomainException("Step order must be non-negative.");
        }

        // Kind/gating/engine consistency:
        //   Agent steps require an EnginePref (router needs something to pick).
        //   InProc steps have no engine; the InProc registry is keyed off step Key.
        //   HumanGate steps are pure pauses; gating must not be Auto.
        if (kind == WorkflowStepKind.Agent && enginePref is null)
        {
            throw new DomainException("Agent steps require an EnginePref.");
        }
        if (kind == WorkflowStepKind.InProc && enginePref is not null)
        {
            throw new DomainException("InProc steps must not declare an EnginePref.");
        }
        if (kind == WorkflowStepKind.HumanGate && gating == WorkflowStepGating.Auto)
        {
            throw new DomainException("HumanGate steps must declare a non-Auto gating role.");
        }

        var step = new WorkflowStep(
            WorkflowStepId.New(),
            workflowId,
            order,
            key.Trim(),
            kind,
            gating,
            enginePref,
            string.IsNullOrWhiteSpace(outputSchemaName) ? null : outputSchemaName.Trim(),
            budgets);
        step._fragmentSelectors.AddRange(selectors);
        return step;
    }
}
