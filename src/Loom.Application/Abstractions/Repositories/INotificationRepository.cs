using Loom.Domain.Notifications;

namespace Loom.Application.Abstractions;

public interface INotificationRepository
{
    Task AddAsync(Notification notification, CancellationToken ct = default);
    Task<Notification?> GetAsync(NotificationId id, CancellationToken ct = default);

    /// <summary>
    /// Bell-icon feed: most recent unread notifications first, then most
    /// recent read ones. Capped by <paramref name="take"/>.
    /// </summary>
    Task<IReadOnlyList<Notification>> ListRecentAsync(Guid userId, int take, CancellationToken ct = default);

    Task<IReadOnlyList<Notification>> ListUnreadAsync(Guid userId, int take, CancellationToken ct = default);

    Task<int> UnreadCountAsync(Guid userId, CancellationToken ct = default);

    void Remove(Notification notification);

    Task<int> DeleteAllForUserAsync(Guid userId, CancellationToken ct = default);
}
