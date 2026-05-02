using Loom.Domain.Artifacts;
using Loom.Domain.Fragments;
using Loom.Domain.Nodes;
using Loom.Domain.Runs;

namespace Loom.Application.Abstractions;

public interface IProjectRepository
{
    Task<Project?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Project?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct = default);
    Task AddAsync(Project project, CancellationToken ct = default);
}

public interface IFragmentRepository
{
    Task<Fragment?> GetAsync(FragmentId id, CancellationToken ct = default);
    Task<Fragment?> GetByKeyAsync(string key, FragmentScope scope, Guid? scopeId, CancellationToken ct = default);
    Task<IReadOnlyList<Fragment>> ListByCategoryAsync(FragmentCategory category, CancellationToken ct = default);
    Task<IReadOnlyList<Fragment>> ListGlobalAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Fragment>> ListByProjectAsync(Guid projectId, CancellationToken ct = default);
    Task AddAsync(Fragment fragment, CancellationToken ct = default);
}

public interface IRunRepository
{
    Task<Run?> GetAsync(RunId id, CancellationToken ct = default);
    Task<IReadOnlyList<Run>> GetByNodeAsync(NodeId nodeId, CancellationToken ct = default);
    Task<IReadOnlyList<Run>> GetActiveAsync(CancellationToken ct = default);
    Task AddAsync(Run run, CancellationToken ct = default);
}

public interface IArtifactRepository
{
    Task<Artifact?> GetAsync(ArtifactId id, CancellationToken ct = default);
    Task<IReadOnlyList<Artifact>> GetByNodeAsync(NodeId nodeId, CancellationToken ct = default);
    Task AddAsync(Artifact artifact, CancellationToken ct = default);
}
