using System.Net.Http.Json;
using Loom.Domain.Nodes;
using Loom.Domain.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loom.Application.Notifications;

/// <summary>
/// Microsoft Teams notification channel using an "Incoming Webhook"
/// connector. Posts an adaptive card per notification. Replaces
/// <see cref="TeamsChannelStub"/> for Phase-3 — single team-wide webhook
/// URL configured via <see cref="TeamsWebhookOptions"/>; per-user routing
/// waits for tenant identity.
///
/// Failure mode: best-effort. Webhook errors are logged but never thrown —
/// in-app + email channels still fire, and the dispatcher retries the row
/// only on exception. We deliberately swallow here so a Teams outage
/// doesn't dead-letter Loom's outbox.
/// </summary>
public sealed class TeamsWebhookChannel(
    HttpClient http,
    IOptionsMonitor<TeamsWebhookOptions> options,
    ILogger<TeamsWebhookChannel> logger) : INotificationChannel
{
    public SubscriptionChannel Channel => SubscriptionChannel.Teams;

    public async Task SendAsync(Guid userId, NodeId nodeId, NotificationPayload payload, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var opts = options.CurrentValue;
        if (string.IsNullOrWhiteSpace(opts.WebhookUrl))
        {
            TeamsLog.NotConfigured(logger, userId, payload.Title);
            return;
        }

        var card = BuildCard(payload, opts);
        try
        {
            using var resp = await http.PostAsJsonAsync(opts.WebhookUrl, card, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var status = (int)resp.StatusCode;
                TeamsLog.PostFailed(logger, status, payload.Title);
            }
        }
        catch (Exception ex)
        {
            TeamsLog.Threw(logger, ex, payload.Title);
        }
    }

    private static object BuildCard(NotificationPayload payload, TeamsWebhookOptions opts)
    {
        string? deepLink = null;
        if (payload.DeepLink is not null)
        {
            if (payload.DeepLink.IsAbsoluteUri)
            {
                deepLink = payload.DeepLink.ToString();
            }
            else if (!string.IsNullOrWhiteSpace(opts.PublicBaseUrl) &&
                     Uri.TryCreate(new Uri(opts.PublicBaseUrl, UriKind.Absolute), payload.DeepLink, out var combined))
            {
                deepLink = combined.ToString();
            }
        }

        var bodyBlocks = new List<object>
        {
            new
            {
                type = "TextBlock",
                text = payload.Title,
                weight = "Bolder",
                size = "Medium",
                wrap = true,
                color = payload.Severity switch
                {
                    NotificationSeverity.Error => "Attention",
                    NotificationSeverity.Warning => "Warning",
                    _ => "Default"
                }
            },
            new { type = "TextBlock", text = payload.Body, wrap = true }
        };

        var actions = new List<object>();
        if (deepLink is not null)
        {
            actions.Add(new
            {
                type = "Action.OpenUrl",
                title = "Open in Loom",
                url = deepLink
            });
        }

        return new
        {
            type = "message",
            attachments = new[]
            {
                new
                {
                    contentType = "application/vnd.microsoft.card.adaptive",
                    content = new
                    {
                        type = "AdaptiveCard",
                        version = "1.4",
                        body = bodyBlocks,
                        actions = actions
                    }
                }
            }
        };
    }
}

internal static partial class TeamsLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "[Teams] no WebhookUrl configured; would notify user {UserId}: {Title}")]
    public static partial void NotConfigured(ILogger logger, Guid userId, string title);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "[Teams] webhook POST failed (HTTP {Status}) for: {Title}")]
    public static partial void PostFailed(ILogger logger, int status, string title);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "[Teams] webhook POST threw for: {Title}")]
    public static partial void Threw(ILogger logger, Exception ex, string title);
}
