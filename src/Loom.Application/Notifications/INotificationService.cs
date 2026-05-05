using Loom.Domain.Nodes;
using Loom.Domain.Notifications;

namespace Loom.Application.Notifications;

/// <summary>
/// Per-user fan-out facade. The outbox dispatcher hands off domain events
/// to <see cref="DispatchAsync"/>, which loads the matching subscriptions
/// and invokes the appropriate channel sender (in-app via SignalR, Teams
/// via Graph, email via SMTP — the latter two are stubbed seams in
/// Phase 3).
/// </summary>
public interface INotificationService
{
    Task DispatchAsync(NodeId nodeId, SubscriptionEventType eventType, NotificationPayload payload, CancellationToken ct = default);
}

/// <summary>
/// What gets delivered. The actual rendering is per-channel; this DTO
/// carries the structured fields each channel needs.
/// </summary>
public sealed record NotificationPayload(
    string Title,
    string Body,
    Uri? DeepLink,
    NotificationSeverity Severity,
    Loom.Domain.Runs.RunId? RunId = null);

/// <summary>
/// Per-channel sender. Phase 3 ships InApp (SignalR) properly; Teams +
/// Email are stubbed seams that log and return — the host wires real
/// implementations in Phase 5+ once tenant-level config lands.
/// </summary>
public interface INotificationChannel
{
    SubscriptionChannel Channel { get; }
    Task SendAsync(Guid userId, NodeId nodeId, NotificationPayload payload, CancellationToken ct = default);
}
