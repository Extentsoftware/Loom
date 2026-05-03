using Loom.Application.Abstractions;
using Loom.Domain.Nodes;
using Loom.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace Loom.Application.Notifications;

/// <summary>
/// Resolves matching subscriptions for an event and fans out to the
/// registered channel senders. Realtime-mode subscriptions deliver
/// immediately; Digest-mode subscriptions are buffered for a Phase-3.5
/// digest cron (out of scope for the kernel slice — they no-op for now).
/// </summary>
public sealed class NotificationService(
    ISubscriptionRepository subscriptions,
    IEnumerable<INotificationChannel> channels,
    ILogger<NotificationService> logger) : INotificationService
{
    private readonly Dictionary<SubscriptionChannel, INotificationChannel> _byChannel =
        channels.ToDictionary(c => c.Channel);

    public async Task DispatchAsync(NodeId nodeId, SubscriptionEventType eventType, NotificationPayload payload, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var matched = await subscriptions.FindForEventAsync(nodeId, eventType, ct);

        foreach (var sub in matched)
        {
            if (sub.Mode != SubscriptionMode.Realtime)
            {
                // Phase-3.5 digest cron: read all SubscriptionMode.Digest
                // rows, batch by user, and send a single notification per
                // hour. The cron lives in Loom.Web hosting, fed by a
                // notification_inbox table that we don't ship in 1C.
                continue;
            }

            if (!_byChannel.TryGetValue(sub.Channel, out var channel))
            {
                NotificationServiceLog.NoChannel(logger, sub.Channel);
                continue;
            }

            try
            {
                await channel.SendAsync(sub.UserId, nodeId, payload, ct);
            }
            catch (Exception ex)
            {
                NotificationServiceLog.SendFailed(logger, ex, sub.Channel, sub.UserId);
            }
        }
    }
}

internal static partial class NotificationServiceLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "No notification channel registered for {Channel}; subscription skipped")]
    public static partial void NoChannel(ILogger logger, SubscriptionChannel channel);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Notification send failed via {Channel} for user {UserId}")]
    public static partial void SendFailed(ILogger logger, Exception ex, SubscriptionChannel channel, Guid userId);
}
