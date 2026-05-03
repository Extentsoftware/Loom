using Loom.Domain.Common;

namespace Loom.Domain.Integrations;

public readonly record struct WebhookDeliveryId(Guid Value) : IEntityId
{
    public static WebhookDeliveryId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString("N");
}

public enum WebhookDeliveryStatus
{
    Received = 1,
    /// <summary>HMAC validation failed; payload was rejected before dispatch.</summary>
    SignatureInvalid = 2,
    Processed = 3,
    Failed = 4
}

/// <summary>
/// Append-only audit row for an inbound webhook. The full body is stored
/// (capped at the column's nvarchar(max)) so engineers can replay deliveries
/// off-line if a downstream handler fails after a deploy.
/// </summary>
public sealed class WebhookDelivery
{
    private WebhookDelivery() { } // EF Core

    private WebhookDelivery(
        WebhookDeliveryId id,
        IntegrationConnectionId? connectionId,
        IntegrationKind kind,
        string eventType,
        string headersJson,
        string bodyJson,
        DateTimeOffset receivedAt)
    {
        Id = id;
        ConnectionId = connectionId;
        Kind = kind;
        EventType = eventType;
        HeadersJson = headersJson;
        BodyJson = bodyJson;
        ReceivedAt = receivedAt;
        Status = WebhookDeliveryStatus.Received;
    }

    public WebhookDeliveryId Id { get; private set; }
    public IntegrationConnectionId? ConnectionId { get; private set; }
    public IntegrationKind Kind { get; private set; }

    /// <summary>Event type as advertised by the external system (e.g. "git.push", "workitem.updated").</summary>
    public string EventType { get; private set; } = null!;

    public string HeadersJson { get; private set; } = null!;
    public string BodyJson { get; private set; } = null!;
    public WebhookDeliveryStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    public static WebhookDelivery Create(
        IntegrationConnectionId? connectionId,
        IntegrationKind kind,
        string eventType,
        string headersJson,
        string bodyJson,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        return new WebhookDelivery(
            WebhookDeliveryId.New(),
            connectionId,
            kind,
            eventType.Trim(),
            headersJson ?? "{}",
            bodyJson ?? string.Empty,
            now);
    }

    public void MarkSignatureInvalid(string reason, DateTimeOffset now)
    {
        Status = WebhookDeliveryStatus.SignatureInvalid;
        FailureReason = reason;
        ProcessedAt = now;
    }

    public void MarkProcessed(DateTimeOffset now)
    {
        Status = WebhookDeliveryStatus.Processed;
        ProcessedAt = now;
    }

    public void MarkFailed(string reason, DateTimeOffset now)
    {
        Status = WebhookDeliveryStatus.Failed;
        FailureReason = reason;
        ProcessedAt = now;
    }
}
