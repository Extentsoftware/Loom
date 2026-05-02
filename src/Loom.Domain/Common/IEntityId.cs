namespace Loom.Domain.Common;

/// <summary>
/// Marker interface for strongly-typed entity IDs.
/// Concrete IDs are readonly record structs wrapping a Guid, see e.g. NodeId.
/// Strongly-typed IDs prevent passing a NodeId where an ArtifactId is expected.
/// </summary>
public interface IEntityId
{
    Guid Value { get; }
}
