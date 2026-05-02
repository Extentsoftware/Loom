namespace Loom.Domain.Common;

/// <summary>
/// Thrown when a domain invariant is violated. These represent programmer errors
/// or attempts to put the model in an impossible state, not user input errors.
/// User input validation belongs at the application boundary.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception inner) : base(message, inner) { }
}
