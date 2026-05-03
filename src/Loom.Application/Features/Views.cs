using Loom.Domain.Nodes;
using Loom.Domain.Runs;

namespace Loom.Application.Features;

/// <summary>
/// Tree view rooted at a project. Recursive: each row knows its immediate
/// children. The web layer renders this directly; SignalR refreshes single
/// rows on NodeUpdated rather than re-fetching the whole tree.
/// </summary>
public sealed record NodeTreeView(
    Guid ProjectId,
    string ProjectName,
    IReadOnlyList<NodeTreeRow> Roots);

public sealed record NodeTreeRow(
    NodeId NodeId,
    NodeId? ParentId,
    string Slug,
    string Title,
    NodeType Type,
    NodePhase Phase,
    StatusSignals Status,
    IReadOnlyList<NodeTreeRow> Children);

/// <summary>
/// Everything the Feature Workspace screen needs in one shot. Pre-resolved
/// effective fragments and recent runs so the page can render without N+1
/// trips. The page subscribes to NodeHub for incremental updates.
/// </summary>
public sealed record FeatureWorkspaceView(
    NodeId NodeId,
    Guid ProjectId,
    NodeId? ParentId,
    string Slug,
    string Title,
    string? Intent,
    NodeType Type,
    NodePhase Phase,
    IReadOnlyList<Outcome> Outcomes,
    IReadOnlyList<Hypothesis> Hypotheses,
    IReadOnlyList<Constraint> Constraints,
    IReadOnlyList<string> OpenQuestions,
    IReadOnlyList<Stakeholder> Stakeholders,
    IReadOnlyList<NodeTreeRow> Children,
    IReadOnlyList<RunSummary> RecentRuns,
    StatusSignals Status);

public sealed record RunSummary(
    RunId RunId,
    EngineName Engine,
    RunState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    decimal? CostUsd);
