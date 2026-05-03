using Loom.Application.Abstractions;
using Loom.Domain.Common.DomainEvents;
using Microsoft.AspNetCore.SignalR;

namespace Loom.Web.Hubs;

/// <summary>
/// Outbox handler that fans node-scoped domain events into the NodeHub. Each
/// handler resolves the hub context for the connection group and pushes a
/// stable client method name. The Razor pages subscribe to those methods
/// directly via the HubConnection client.
///
/// Method names sent to clients:
///   "node-updated"        — the node's fields changed
///   "node-phase-advanced" — phase moved (also rendered as a node-update)
///   "discovery-accepted"  — PO gate resolved on this node
///   "run-state-changed"   — run state moved on this node
/// </summary>
public sealed class NodeHubBroadcaster(IHubContext<NodeHub> hub) :
    IDomainEventHandler<NodeUpdated>,
    IDomainEventHandler<NodeCreated>,
    IDomainEventHandler<NodePhaseAdvanced>,
    IDomainEventHandler<DiscoveryAccepted>,
    IDomainEventHandler<RunStarted>,
    IDomainEventHandler<RunCompleted>,
    IDomainEventHandler<RunFailed>,
    IDomainEventHandler<RunPausedForHuman>
{
    public Task HandleAsync(NodeUpdated evt, CancellationToken ct = default) =>
        hub.Clients.Group(NodeHub.GroupName(evt.NodeId.Value))
            .SendAsync("node-updated", new { nodeId = evt.NodeId.Value, occurredAt = evt.OccurredAt }, ct);

    public Task HandleAsync(NodeCreated evt, CancellationToken ct = default) =>
        // Tree-rendering pages join the project's root and re-fetch the tree
        // when a node is created; per-node groups don't help here. Phase 3
        // adds a per-project group.
        Task.CompletedTask;

    public Task HandleAsync(NodePhaseAdvanced evt, CancellationToken ct = default) =>
        hub.Clients.Group(NodeHub.GroupName(evt.NodeId.Value))
            .SendAsync("node-phase-advanced", new { nodeId = evt.NodeId.Value, from = evt.From.ToString(), to = evt.To.ToString() }, ct);

    public Task HandleAsync(DiscoveryAccepted evt, CancellationToken ct = default) =>
        hub.Clients.Group(NodeHub.GroupName(evt.NodeId.Value))
            .SendAsync("discovery-accepted", new { nodeId = evt.NodeId.Value }, ct);

    public Task HandleAsync(RunStarted evt, CancellationToken ct = default) =>
        // Run events fan to all subscribed groups; the client filters by run id
        // via SignalR's All-with-filter pattern in Phase 3+.
        hub.Clients.All.SendAsync("run-state-changed", new { runId = evt.RunId.Value, state = "running", externalRunId = evt.ExternalRunId }, ct);

    public Task HandleAsync(RunCompleted evt, CancellationToken ct = default) =>
        hub.Clients.All.SendAsync("run-state-changed", new { runId = evt.RunId.Value, state = "completed", costUsd = evt.CostUsd }, ct);

    public Task HandleAsync(RunFailed evt, CancellationToken ct = default) =>
        hub.Clients.All.SendAsync("run-state-changed", new { runId = evt.RunId.Value, state = "failed", reason = evt.Reason }, ct);

    public Task HandleAsync(RunPausedForHuman evt, CancellationToken ct = default) =>
        hub.Clients.All.SendAsync("run-state-changed", new { runId = evt.RunId.Value, state = "paused", stepKey = evt.StepKey, role = evt.GatingRole.ToString() }, ct);
}
