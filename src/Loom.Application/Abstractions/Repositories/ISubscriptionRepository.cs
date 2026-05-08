using Loom.Domain.Common.DomainEvents;
using Loom.Domain.Nodes;
using Loom.Domain.Notifications;

namespace Loom.Application.Abstractions;

public interface ISubscriptionRepository
{
    Task<Subscription?> GetAsync(SubscriptionId id, CancellationToken ct = default);

    /// <summary>
    /// Find subscriptions matching an event. Returns rows that subscribe
    /// either to this exact node or to its containing project, with an
    /// optional role narrowing applied:
    /// <list type="bullet">
    /// <item>row.Role == null matches any role (or events without one);</item>
    /// <item>row.Role == eventRole matches;</item>
    /// <item>otherwise skipped.</item>
    /// </list>
    /// </summary>
    Task<IReadOnlyList<Subscription>> FindForEventAsync(
        NodeId nodeId,
        Guid projectId,
        SubscriptionEventType eventType,
        WorkflowStepGatingRole? eventRole,
        CancellationToken ct = default);

    Task<IReadOnlyList<Subscription>> ListByUserAsync(Guid userId, CancellationToken ct = default);

    Task AddAsync(Subscription subscription, CancellationToken ct = default);
    void Remove(Subscription subscription);
}
