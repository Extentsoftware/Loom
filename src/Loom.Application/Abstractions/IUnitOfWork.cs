namespace Loom.Application.Abstractions;

/// <summary>
/// Coordinates persistence across repositories within a transactional boundary.
/// Implemented by the EF Core <c>DbContext</c> in Infrastructure. Application
/// services accept <see cref="IUnitOfWork"/> rather than the DbContext so they
/// stay free of EF concerns.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
