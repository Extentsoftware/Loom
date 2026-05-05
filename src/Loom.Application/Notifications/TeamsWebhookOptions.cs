namespace Loom.Application.Notifications;

/// <summary>
/// Configuration for the Teams "Incoming Webhook" notification channel.
/// Bound from <c>Loom:Notifications:Teams</c>. The webhook URL is created
/// in Teams under "Connectors" → "Incoming Webhook" and is shared per
/// channel — Phase-3 ships a single team-wide URL; per-user routing waits
/// for tenant identity to land.
///
/// <see cref="PublicBaseUrl"/> is used to convert relative deep links from
/// <c>NotificationPayload</c> into absolute URLs the Teams client can open.
/// If unset, links are dropped from the card.
/// </summary>
public sealed class TeamsWebhookOptions
{
    public const string SectionName = "Loom:Notifications:Teams";

    /// <summary>The Incoming Webhook URL. When null/empty the channel logs
    /// and returns (no-op), preserving the previous stub behaviour for
    /// hosts that haven't configured Teams yet.</summary>
    public string? WebhookUrl { get; set; }

    /// <summary>Base URL for converting relative deep-links to absolute.
    /// e.g. "https://loom.contoso.com". When null/empty, links are omitted.</summary>
    public string? PublicBaseUrl { get; set; }
}
