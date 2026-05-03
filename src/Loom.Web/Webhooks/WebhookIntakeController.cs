using Loom.Application.Abstractions;
using Loom.Domain.Integrations;
using Loom.Integrations.Webhooks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loom.Web.Webhooks;

/// <summary>
/// Intake endpoints for inbound webhooks. Each route validates the
/// signature against the matching IntegrationConnection's stored shared
/// secret, then writes a WebhookDelivery audit row. Phase-3+ will wire
/// the dispatch into domain handlers; Phase-2 only proves the rejection +
/// audit path so engineers can light up real systems incrementally.
///
/// Routes are anonymous because external systems can't carry the host's
/// auth cookies; the HMAC IS the auth.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("webhooks")]
public sealed class WebhookIntakeController(
    IIntegrationConnectionRepository connections,
    IWebhookDeliveryRepository deliveries,
    IUnitOfWork uow,
    ISystemClock clock) : ControllerBase
{
    /// <summary>
    /// ADO push notifications carry the signature in <c>X-Vss-Hmac-Sha256</c>
    /// (raw base64 digest). Match by organisation/project routed through the
    /// Connection's TenantOrAccount field.
    /// </summary>
    [HttpPost("ado/{connectionId:guid}")]
    public async Task<IActionResult> Ado(
        [FromRoute] Guid connectionId,
        [FromHeader(Name = "X-Vss-Hmac-Sha256")] string? signature,
        [FromHeader(Name = "X-Vss-EventType")] string? eventType,
        CancellationToken ct)
    {
        return await HandleAsync(
            new IntegrationConnectionId(connectionId),
            IntegrationKind.AzureDevOps,
            signature,
            eventType,
            ct);
    }

    /// <summary>
    /// GitHub push notifications carry the signature in
    /// <c>x-hub-signature-256</c> as <c>sha256=&lt;hex&gt;</c>; the validator
    /// accepts both shapes.
    /// </summary>
    [HttpPost("github/{connectionId:guid}")]
    public async Task<IActionResult> GitHub(
        [FromRoute] Guid connectionId,
        [FromHeader(Name = "X-Hub-Signature-256")] string? signature,
        [FromHeader(Name = "X-GitHub-Event")] string? eventType,
        CancellationToken ct)
    {
        return await HandleAsync(
            new IntegrationConnectionId(connectionId),
            IntegrationKind.GitHub,
            signature,
            eventType,
            ct);
    }

    private async Task<IActionResult> HandleAsync(
        IntegrationConnectionId connectionId,
        IntegrationKind kind,
        string? signature,
        string? eventType,
        CancellationToken ct)
    {
        var connection = await connections.GetAsync(connectionId, ct);
        if (connection is null || connection.Kind != kind)
        {
            // Don't leak existence; return 404 either way.
            return NotFound();
        }

        var body = await ReadBodyAsync(ct);
        var headersJson = SerializeHeaders(Request);

        var delivery = WebhookDelivery.Create(
            connectionId,
            kind,
            eventType ?? "unknown",
            headersJson,
            System.Text.Encoding.UTF8.GetString(body),
            clock.UtcNow);

        if (string.IsNullOrEmpty(connection.WebhookSecret) ||
            !HmacSignatureValidator.Validate(signature ?? string.Empty, body, connection.WebhookSecret))
        {
            delivery.MarkSignatureInvalid("HMAC validation failed", clock.UtcNow);
            await deliveries.AddAsync(delivery, ct);
            await uow.SaveChangesAsync(ct);
            return Unauthorized();
        }

        // Phase-2: just record receipt. Phase-3+ will dispatch to the
        // domain-event pipeline (e.g. emit RepoPushed → handlers update
        // Feature Workspace activity strip).
        delivery.MarkProcessed(clock.UtcNow);
        await deliveries.AddAsync(delivery, ct);
        await uow.SaveChangesAsync(ct);
        return Accepted();
    }

    private async Task<byte[]> ReadBodyAsync(CancellationToken ct)
    {
        Request.EnableBuffering();
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms, ct);
        Request.Body.Position = 0;
        return ms.ToArray();
    }

    private static string SerializeHeaders(HttpRequest request)
    {
        var dict = request.Headers.ToDictionary(
            h => h.Key,
            h => h.Value.ToString());
        return System.Text.Json.JsonSerializer.Serialize(dict);
    }
}
