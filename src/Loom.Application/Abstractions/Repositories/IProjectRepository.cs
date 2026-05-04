using Loom.Domain.Nodes;

namespace Loom.Application.Abstractions;

public interface IProjectRepository
{
    Task<Project?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Project?> GetBySlugAsync(string slug, CancellationToken ct = default);

    /// <summary>
    /// List non-archived projects, ordered by name.
    /// </summary>
    Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct = default);

    /// <summary>
    /// List projects including archived ones. Used by Operating Picture's
    /// "Show archived" toggle and any audit/admin path.
    /// </summary>
    Task<IReadOnlyList<Project>> ListAllAsync(CancellationToken ct = default);

    Task AddAsync(Project project, CancellationToken ct = default);
}
