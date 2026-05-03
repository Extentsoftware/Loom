using Loom.Application.Notifications;
using Loom.Domain.Nodes;
using Loom.Domain.Notifications;
using Microsoft.AspNetCore.SignalR;

namespace Loom.Web.Hubs;

/// <summary>
/// In-app notification channel: pushes the payload to the user's NodeHub
/// connection-group (SignalR group keyed by user id, joined when the
/// user authenticates). Phase 3 ships this; the per-user NotificationHub
/// from design §10 lands in Phase 6 alongside the digest cron.
/// </summary>
public sealed class InAppNotificationChannel(IHubContext<NodeHub> hub) : INotificationChannel
{
    public SubscriptionChannel Channel => SubscriptionChannel.InApp;

    public Task SendAsync(Guid userId, NodeId nodeId, NotificationPayload payload, CancellationToken ct = default) =>
        hub.Clients.User(userId.ToString()).SendAsync(
            "notification",
            new
            {
                title = payload.Title,
                body = payload.Body,
                deepLink = payload.DeepLink?.ToString(),
                severity = payload.Severity.ToString(),
                nodeId = nodeId.Value
            },
            ct);
}
