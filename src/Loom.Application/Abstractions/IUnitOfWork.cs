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

    /// <summary>
    /// Begin an explicit transaction so multiple SaveChanges calls (and
    /// the outbox events drained from each) commit or roll back as a
    /// single atomic unit. Used by orchestration paths like accept-
    /// decompose where a partial failure leaves the user stuck — see
    /// <see cref="IUnitOfWorkTransaction"/>.
    /// </summary>
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default);
}

/// <summary>
/// Handle to an in-flight transaction opened via
/// <see cref="IUnitOfWork.BeginTransactionAsync"/>. The implementation
/// wraps EF Core's <c>IDbContextTransaction</c>; failure to commit before
/// disposing rolls back automatically.
/// </summary>
public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}
