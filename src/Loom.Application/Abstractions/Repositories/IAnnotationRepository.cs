using Loom.Domain.Annotations;

namespace Loom.Application.Abstractions;

public interface IAnnotationRepository
{
    Task<Annotation?> GetAsync(AnnotationId id, CancellationToken ct = default);
    Task<IReadOnlyList<Annotation>> GetByArtifactAsync(Guid artifactId, CancellationToken ct = default);
    Task AddAsync(Annotation annotation, CancellationToken ct = default);
}
