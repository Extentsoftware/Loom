using Loom.Domain.Common;
using Loom.Domain.Nodes;
using Loom.Domain.Runs;

namespace Loom.Domain.Notifications;

public readonly record struct NotificationId(Guid Value) : IEntityId
{
    public static NotificationId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString("N");
}

/// <summary>
/// A persisted, per-user notification record. Written by the InApp channel
/// when a domain event matches a user's subscription, then surfaced in the
/// bell-icon feed and pushed live via SignalR.
///
/// Marked-read state is tracked here rather than on the channel side so a
/// user's read state survives reconnects and tab switches.
/// </summary>
public sealed class Notification
{
    private Notification() { }

    public NotificationId Id { get; private set; }
    public Guid UserId { get; private set; }
    public NodeId? NodeId { get; private set; }
    public RunId? RunId { get; private set; }
    public SubscriptionEventType EventType { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string? DeepLink { get; private set; }
    public NotificationSeverity Severity { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }

    public static Notification Create(
        Guid userId,
        NodeId? nodeId,
        RunId? runId,
        SubscriptionEventType eventType,
        string title,
        string body,
        string? deepLink,
        NotificationSeverity severity,
        DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("Notification UserId must be a non-empty GUID.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        return new Notification
        {
            Id = NotificationId.New(),
            UserId = userId,
            NodeId = nodeId,
            RunId = runId,
            EventType = eventType,
            Title = title.Trim(),
            Body = body.Trim(),
            DeepLink = string.IsNullOrWhiteSpace(deepLink) ? null : deepLink.Trim(),
            Severity = severity,
            CreatedAt = now,
            ReadAt = null
        };
    }

    public void MarkRead(DateTimeOffset now)
    {
        ReadAt ??= now;
    }
}

public enum NotificationSeverity
{
    Info = 1,
    Warning = 2,
    Error = 3
}
