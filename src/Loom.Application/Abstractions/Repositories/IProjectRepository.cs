using Loom.Domain.Nodes;

namespace Loom.Application.Abstractions;

public interface IProjectRepository
{
    Task<Project?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Project?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct = default);
    Task AddAsync(Project project, CancellationToken ct = default);
}
