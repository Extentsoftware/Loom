using Loom.Domain.Integrations;

namespace Loom.Application.Abstractions;

public interface IIntegrationConnectionRepository
{
    Task<IntegrationConnection?> GetAsync(IntegrationConnectionId id, CancellationToken ct = default);
    Task<IReadOnlyList<IntegrationConnection>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<IntegrationConnection>> ListByKindAsync(IntegrationKind kind, CancellationToken ct = default);
    Task<IReadOnlyList<IntegrationConnection>> ListByProjectAsync(Guid? projectId, CancellationToken ct = default);
    Task AddAsync(IntegrationConnection connection, CancellationToken ct = default);
}
