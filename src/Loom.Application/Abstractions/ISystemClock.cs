namespace Loom.Application.Abstractions;

/// <summary>
/// Abstraction over <see cref="DateTimeOffset.UtcNow"/> so domain code stays
/// deterministic and tests can pin time. The default implementation in
/// Infrastructure simply returns UtcNow.
/// </summary>
public interface ISystemClock
{
    DateTimeOffset UtcNow { get; }
}
