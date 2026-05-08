using Loom.Domain.Common.DomainEvents;
using Loom.Domain.Nodes;
using Loom.Domain.Notifications;

namespace Loom.Application.Notifications;

public interface ISubscriptionService
{
    /// <summary>
    /// Subscribe to events on a single node. Idempotent on
    /// (userId, nodeId, eventType, channel, role); calling twice with a
    /// different mode updates the mode.
    /// </summary>
    Task<Subscription> SubscribeNodeAsync(
        Guid userId,
        NodeId nodeId,
        SubscriptionEventType eventType,
        SubscriptionChannel channel,
        SubscriptionMode mode,
        WorkflowStepGatingRole? role = null,
        CancellationToken ct = default);

    /// <summary>
    /// Subscribe to events on every node in a project. Useful for role-
    /// scoped wide subscriptions ("ping me on every UX gate in this
    /// project").
    /// </summary>
    Task<Subscription> SubscribeProjectAsync(
        Guid userId,
        Guid projectId,
        SubscriptionEventType eventType,
        SubscriptionChannel channel,
        SubscriptionMode mode,
        WorkflowStepGatingRole? role = null,
        CancellationToken ct = default);

    /// <summary>
    /// Subscribe globally — fires for every matching event regardless of
    /// node or project. Pair with a Role for "page me on any UX gate
    /// anywhere" type rules.
    /// </summary>
    Task<Subscription> SubscribeGlobalAsync(
        Guid userId,
        SubscriptionEventType eventType,
        SubscriptionChannel channel,
        SubscriptionMode mode,
        WorkflowStepGatingRole? role = null,
        CancellationToken ct = default);

    Task UnsubscribeAsync(SubscriptionId id, CancellationToken ct = default);
    Task<IReadOnlyList<Subscription>> ListMineAsync(Guid userId, CancellationToken ct = default);
}
