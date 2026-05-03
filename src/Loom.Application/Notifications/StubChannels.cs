using Loom.Domain.Nodes;
using Loom.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace Loom.Application.Notifications;

/// <summary>
/// Phase-3 placeholder Teams + Email channels. They log the would-be
/// payload and return; real implementations land in Phase 5 (Teams via
/// Microsoft.Graph) and Phase 5+ (email via SMTP / Graph). The seam keeps
/// the dispatcher honest in the meantime — subscriptions configured for
/// these channels appear in the audit log even when the integration
/// hasn't been turned on.
/// </summary>
public sealed class TeamsChannelStub(ILogger<TeamsChannelStub> logger) : INotificationChannel
{
    public SubscriptionChannel Channel => SubscriptionChannel.Teams;

    public Task SendAsync(Guid userId, NodeId nodeId, NotificationPayload payload, CancellationToken ct = default)
    {
        TeamsChannelLog.Stubbed(logger, userId, nodeId.Value, payload.Title);
        return Task.CompletedTask;
    }
}

public sealed class EmailChannelStub(ILogger<EmailChannelStub> logger) : INotificationChannel
{
    public SubscriptionChannel Channel => SubscriptionChannel.Email;

    public Task SendAsync(Guid userId, NodeId nodeId, NotificationPayload payload, CancellationToken ct = default)
    {
        EmailChannelLog.Stubbed(logger, userId, nodeId.Value, payload.Title);
        return Task.CompletedTask;
    }
}

internal static partial class TeamsChannelLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "[Teams stub] would notify user {UserId} on node {NodeId}: {Title}")]
    public static partial void Stubbed(ILogger logger, Guid userId, Guid nodeId, string title);
}

internal static partial class EmailChannelLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "[Email stub] would notify user {UserId} on node {NodeId}: {Title}")]
    public static partial void Stubbed(ILogger logger, Guid userId, Guid nodeId, string title);
}
