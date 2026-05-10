using Loom.Domain.Common;
using Loom.Domain.Runs;

namespace Loom.Domain.Workflows;

/// <summary>
/// A versioned, immutable definition of a process Loom can run. Identity is
/// (Key, Version). Workflows are seeded into MSSQL by the bootstrapper or
/// authored through the Workflow Designer (Phase 7). Once a Workflow is
/// referenced by a Run, its Steps are frozen in time — to change the workflow,
/// publish a new version.
///
/// Phase 1 ships exactly one workflow ("kickoff") in code; Phase 7 introduces
/// per-project pinning and methodology-owner authoring.
/// </summary>
public sealed class Workflow
{
    private readonly List<WorkflowStep> _steps = [];

    private Workflow() { } // EF Core

    private Workflow(
        WorkflowId id,
        Slug key,
        int version,
        string title,
        DateTimeOffset createdAt)
    {
        Id = id;
        Key = key;
        Version = version;
        Title = title;
        CreatedAt = createdAt;
    }

    public WorkflowId Id { get; private set; }
    public Slug Key { get; private set; }
    public int Version { get; private set; }
    public string Title { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyList<WorkflowStep> Steps => _steps.OrderBy(s => s.Order).ToList();

    public static Workflow Create(
        Slug key,
        int version,
        string title,
        IEnumerable<WorkflowStepDraft> stepDrafts,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(stepDrafts);
        if (version < 1)
        {
            throw new DomainException("Workflow version must be 1 or greater.");
        }

        var workflow = new Workflow(WorkflowId.New(), key, version, title.Trim(), now);

        var order = 0;
        foreach (var draft in stepDrafts)
        {
            ArgumentNullException.ThrowIfNull(draft);
            workflow._steps.Add(WorkflowStep.Create(
                workflow.Id,
                order++,
                draft.Key,
                draft.Kind,
                draft.Gating,
                draft.EnginePref,
                draft.OutputSchemaName,
                draft.Budgets,
                draft.Selectors));
        }

        if (workflow._steps.Count == 0)
        {
            throw new DomainException("A workflow must have at least one step.");
        }

        return workflow;
    }

    public WorkflowStep? FindStep(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _steps.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.Ordinal));
    }
}

/// <summary>
/// Draft shape used to build a Workflow. The aggregate enforces ordering and
/// id assignment; callers describe what they want, not how to construct it.
/// </summary>
public sealed record WorkflowStepDraft(
    string Key,
    WorkflowStepKind Kind,
    WorkflowStepGating Gating,
    EngineName? EnginePref,
    string? OutputSchemaName,
    Budgets Budgets,
    IReadOnlyList<FragmentSelector> Selectors);
