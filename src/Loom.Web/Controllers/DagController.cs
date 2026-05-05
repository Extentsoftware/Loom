using Loom.Application.Abstractions;
using Loom.Domain.Runs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loom.Web.Controllers;

/// <summary>
/// JSON feed for the project DAG dashboard. Returns nodes (id, title, type,
/// phase, run-status hint) and edges (parent → child) for the cytoscape
/// renderer to lay out. Phase-4 (skinny): straight read; future iterations
/// add caching + an aggregate status field that fans the recent-run lookup
/// out across the whole project in one query.
/// </summary>
[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/dag")]
public sealed class DagController(
    IFeatureNodeRepository nodes,
    IRunRepository runs) : ControllerBase
{
    [HttpGet]
    public async Task<DagResponse> GetAsync(Guid projectId, CancellationToken ct = default)
    {
        var allNodes = await nodes.GetByProjectAsync(projectId, ct);

        var nodeDtos = new List<DagNode>(allNodes.Count);
        foreach (var n in allNodes)
        {
            var nodeRuns = await runs.GetByNodeAsync(n.Id, ct);
            var runStatus = SummariseRuns(nodeRuns);
            nodeDtos.Add(new DagNode(
                Id: n.Id.Value,
                Title: n.Title,
                Slug: n.Slug.Value,
                Type: n.Type.ToString(),
                Phase: n.Phase.ToString(),
                RunStatus: runStatus,
                ParentId: n.ParentId?.Value));
        }

        var edges = allNodes
            .Where(n => n.ParentId.HasValue)
            .Select(n => new DagEdge(n.ParentId!.Value.Value, n.Id.Value))
            .ToList();

        return new DagResponse(nodeDtos, edges);
    }

    private static string SummariseRuns(IReadOnlyList<Run> nodeRuns)
    {
        if (nodeRuns.Count == 0) return "none";
        if (nodeRuns.Any(r => r.State == RunState.PausedForHuman)) return "paused";
        if (nodeRuns.Any(r => r.State == RunState.Running || r.State == RunState.Queued)) return "active";
        if (nodeRuns.Any(r => r.State == RunState.Failed)) return "failed";
        if (nodeRuns.Any(r => r.State == RunState.Completed)) return "completed";
        return "other";
    }

    public sealed record DagResponse(IReadOnlyList<DagNode> Nodes, IReadOnlyList<DagEdge> Edges);
    public sealed record DagNode(Guid Id, string Title, string Slug, string Type, string Phase, string RunStatus, Guid? ParentId);
    public sealed record DagEdge(Guid Source, Guid Target);
}
