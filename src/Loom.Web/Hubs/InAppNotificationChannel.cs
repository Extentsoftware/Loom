using Loom.Application.Abstractions;
using Loom.Application.Notifications;
using Loom.Domain.Nodes;
using Loom.Domain.Notifications;
using Microsoft.AspNetCore.SignalR;

namespace Loom.Web.Hubs;

/// <summary>
/// In-app notification channel: persists a <see cref="Notification"/> row
/// (so the bell-icon feed survives reload) and pushes a live event over
/// <see cref="NotificationHub"/> to the user's group. Persistence lands in
/// the same DI scope as the outbox dispatcher's tick, so the dispatcher's
/// final SaveChangesAsync commits both the row and the outbox progress
/// together.
/// </summary>
public sealed class InAppNotificationChannel(
    INotificationRepository repo,
    ISystemClock clock,
    IHubContext<NotificationHub> hub) : INotificationChannel
{
    public SubscriptionChannel Channel => SubscriptionChannel.InApp;

    public async Task SendAsync(Guid userId, NodeId nodeId, NotificationPayload payload, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var entity = Notification.Create(
            userId: userId,
            nodeId: nodeId,
            runId: payload.RunId,
            // Channel sees the rendered payload, not the original event;
            // back-derive a feed-display event-type from severity for now.
            // The handler that called us already had the right type — a
            // future tweak is to push it into the payload too.
            eventType: payload.Severity switch
            {
                NotificationSeverity.Error => SubscriptionEventType.RunFailed,
                NotificationSeverity.Warning => SubscriptionEventType.RunPaused,
                _ => SubscriptionEventType.NodeUpdated
            },
            title: payload.Title,
            body: payload.Body,
            deepLink: payload.DeepLink?.ToString(),
            severity: payload.Severity,
            now: clock.UtcNow);

        await repo.AddAsync(entity, ct);

        await hub.Clients.Group(NotificationHub.GroupName(userId)).SendAsync(
            "notification",
            new
            {
                id = entity.Id.Value,
                title = entity.Title,
                body = entity.Body,
                deepLink = entity.DeepLink,
                severity = entity.Severity.ToString(),
                nodeId = nodeId.Value,
                runId = entity.RunId?.Value,
                createdAt = entity.CreatedAt
            },
            ct);
    }
}
