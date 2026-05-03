using Loom.Domain.Integrations;

namespace Loom.Application.Abstractions;

public interface IWebhookDeliveryRepository
{
    Task<WebhookDelivery?> GetAsync(WebhookDeliveryId id, CancellationToken ct = default);
    Task<IReadOnlyList<WebhookDelivery>> GetByConnectionAsync(IntegrationConnectionId connectionId, int take, CancellationToken ct = default);
    Task AddAsync(WebhookDelivery delivery, CancellationToken ct = default);
}
