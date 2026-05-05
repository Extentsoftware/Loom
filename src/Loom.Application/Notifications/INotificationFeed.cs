using Loom.Domain.Notifications;

namespace Loom.Application.Notifications;

/// <summary>
/// Per-user feed read/mutate facade backing the bell-icon dropdown and the
/// /notifications page. Persistence lives behind <see cref="Loom.Application.Abstractions.INotificationRepository"/>;
/// the InApp channel writes rows, this service reads + mutates them.
/// </summary>
public interface INotificationFeed
{
    Task<IReadOnlyList<Notification>> ListRecentAsync(Guid userId, int take, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> ListUnreadAsync(Guid userId, int take, CancellationToken ct = default);
    Task<int> UnreadCountAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Mark a single notification read. No-op if already read or
    /// not owned by the requesting user (silently ignored to keep the
    /// caller path simple — it's not a security boundary).</summary>
    Task MarkReadAsync(NotificationId id, Guid userId, CancellationToken ct = default);

    /// <summary>Mark every unread notification for the user as read.</summary>
    Task MarkAllReadAsync(Guid userId, CancellationToken ct = default);
}
