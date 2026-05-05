using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Loom.Web.Hubs;

/// <summary>
/// Per-user real-time notification feed. Clients call <see cref="JoinUser"/>
/// with their Loom user id on connect to receive notifications targeted at
/// them. Persistence is handled by <see cref="InAppNotificationChannel"/>;
/// this hub only handles the live push.
///
/// Auth note: in dev the user id is whatever the bell-icon component holds
/// (single-user mode). Real per-user auth lands when identity does — see
/// the Identity follow-up in the roadmap. Until then, anyone connected to
/// this hub can join any user's group, which is fine for single-tenant dev.
/// </summary>
[Authorize]
public sealed class NotificationHub : Hub
{
    public Task JoinUser(Guid userId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(userId));

    public Task LeaveUser(Guid userId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(userId));

    public static string GroupName(Guid userId) => $"user:{userId:N}";
}
