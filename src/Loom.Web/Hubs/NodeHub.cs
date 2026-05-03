using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Loom.Web.Hubs;

/// <summary>
/// Per-node real-time channel. Clients join the group for a specific node id
/// (typically when navigating to its workspace) and receive broadcast events
/// scoped to that node — `NodeUpdated` after edits, `RunStateChanged` as
/// runs progress, `DiscoveryAccepted` when a PO gate resolves.
///
/// AgentHub (cross-node ops view) and NotificationHub (per-user inbox) wait
/// for Phase 3+; the NodeHub alone covers Phase 1's Operating Picture and
/// Feature Workspace screens.
/// </summary>
[Authorize]
public sealed class NodeHub : Hub
{
    public Task JoinNode(Guid nodeId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(nodeId));

    public Task LeaveNode(Guid nodeId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(nodeId));

    public static string GroupName(Guid nodeId) => $"node:{nodeId:N}";
}
