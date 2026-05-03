using Loom.Domain.Common;
using Loom.Domain.Nodes;

namespace Loom.Domain.Notifications;

public readonly record struct SubscriptionId(Guid Value) : IEntityId
{
    public static SubscriptionId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString("N");
}

public enum SubscriptionEventType
{
    /// <summary>Any change to the node's fields or children.</summary>
    NodeUpdated = 1,
    /// <summary>A run on this node hit a human gate.</summary>
    RunPaused = 2,
    /// <summary>A run on this node completed.</summary>
    RunCompleted = 3,
    /// <summary>A run on this node failed.</summary>
    RunFailed = 4
}

public enum SubscriptionChannel
{
    /// <summary>In-app notification feed via SignalR.</summary>
    InApp = 1,
    /// <summary>Microsoft Teams adaptive card via Graph API.</summary>
    Teams = 2,
    /// <summary>Email digest (cron-batched).</summary>
    Email = 3
}

public enum SubscriptionMode
{
    /// <summary>Deliver each event as it happens.</summary>
    Realtime = 1,
    /// <summary>Bundle events into a per-hour digest.</summary>
    Digest = 2
}

/// <summary>
/// Per-user subscription preference: deliver matching events on a given
/// node through the chosen channel and cadence. Compound-unique on
/// (UserId, NodeId, EventType, Channel) — one row decides delivery for
/// that triple. Multiple rows mean the user gets the same event through
/// multiple channels.
/// </summary>
public sealed class Subscription
{
    private Subscription() { } // EF Core

    private Subscription(
        SubscriptionId id,
        Guid userId,
        NodeId nodeId,
        SubscriptionEventType eventType,
        SubscriptionChannel channel,
        SubscriptionMode mode,
        DateTimeOffset createdAt)
    {
        Id = id;
        UserId = userId;
        NodeId = nodeId;
        EventType = eventType;
        Channel = channel;
        Mode = mode;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public SubscriptionId Id { get; private set; }
    public Guid UserId { get; private set; }
    public NodeId NodeId { get; private set; }
    public SubscriptionEventType EventType { get; private set; }
    public SubscriptionChannel Channel { get; private set; }
    public SubscriptionMode Mode { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Subscription Create(
        Guid userId,
        NodeId nodeId,
        SubscriptionEventType eventType,
        SubscriptionChannel channel,
        SubscriptionMode mode,
        DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("UserId is required.");
        }
        return new Subscription(SubscriptionId.New(), userId, nodeId, eventType, channel, mode, now);
    }

    public void ChangeMode(SubscriptionMode mode, DateTimeOffset now)
    {
        if (Mode == mode)
        {
            return;
        }
        Mode = mode;
        UpdatedAt = now;
    }
}
