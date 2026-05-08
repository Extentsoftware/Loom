using Loom.Domain.Common;
using Loom.Domain.Common.DomainEvents;
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
/// Per-user subscription preference. A subscription is scoped to either a
/// single node or a whole project (exactly one of NodeId/ProjectId is set).
/// An optional Role narrows the match further: when set, only events with
/// a matching gating role trigger delivery — currently only RunPaused
/// events carry a gating role; other event types ignore the filter.
///
/// Compound-unique on (UserId, NodeId, ProjectId, EventType, Channel, Role)
/// — one row decides delivery for that combination. Multiple rows mean
/// the user gets the same event through multiple channels.
/// </summary>
public sealed class Subscription
{
    private Subscription() { } // EF Core

    private Subscription(
        SubscriptionId id,
        Guid userId,
        NodeId? nodeId,
        Guid? projectId,
        SubscriptionEventType eventType,
        SubscriptionChannel channel,
        SubscriptionMode mode,
        WorkflowStepGatingRole? role,
        DateTimeOffset createdAt)
    {
        Id = id;
        UserId = userId;
        NodeId = nodeId;
        ProjectId = projectId;
        EventType = eventType;
        Channel = channel;
        Mode = mode;
        Role = role;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public SubscriptionId Id { get; private set; }
    public Guid UserId { get; private set; }
    public NodeId? NodeId { get; private set; }
    public Guid? ProjectId { get; private set; }
    public SubscriptionEventType EventType { get; private set; }
    public SubscriptionChannel Channel { get; private set; }
    public SubscriptionMode Mode { get; private set; }
    /// <summary>
    /// Optional role filter. Null = match any role. When set, the
    /// subscription only fires for events that carry this gating role
    /// (RunPaused today; future role-aware events later).
    /// </summary>
    public WorkflowStepGatingRole? Role { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Subscription CreateForNode(
        Guid userId,
        NodeId nodeId,
        SubscriptionEventType eventType,
        SubscriptionChannel channel,
        SubscriptionMode mode,
        WorkflowStepGatingRole? role,
        DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("UserId is required.");
        }
        return new Subscription(SubscriptionId.New(), userId, nodeId, projectId: null, eventType, channel, mode, role, now);
    }

    public static Subscription CreateForProject(
        Guid userId,
        Guid projectId,
        SubscriptionEventType eventType,
        SubscriptionChannel channel,
        SubscriptionMode mode,
        WorkflowStepGatingRole? role,
        DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("UserId is required.");
        }
        if (projectId == Guid.Empty)
        {
            throw new DomainException("ProjectId is required.");
        }
        return new Subscription(SubscriptionId.New(), userId, nodeId: null, projectId, eventType, channel, mode, role, now);
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
