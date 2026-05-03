using Loom.Domain.Fragments;

namespace Loom.Application.Abstractions;

public interface IFragmentRepository
{
    Task<Fragment?> GetAsync(FragmentId id, CancellationToken ct = default);
    Task<Fragment?> GetByKeyAsync(string key, FragmentScope scope, Guid? scopeId, CancellationToken ct = default);
    Task<IReadOnlyList<Fragment>> ListByCategoryAsync(FragmentCategory category, CancellationToken ct = default);
    Task<IReadOnlyList<Fragment>> ListGlobalAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Fragment>> ListByProjectAsync(Guid projectId, CancellationToken ct = default);
    Task<IReadOnlyList<Fragment>> ListByNodeAsync(Guid nodeId, CancellationToken ct = default);
    Task AddAsync(Fragment fragment, CancellationToken ct = default);
}
