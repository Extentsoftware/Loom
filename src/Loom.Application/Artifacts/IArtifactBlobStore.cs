using Loom.Domain.Artifacts;

namespace Loom.Application.Artifacts;

/// <summary>
/// Storage seam for artifact byte payloads (ADR-0018). The default
/// implementation stores MessagePack-serialized <see cref="ArtifactBundle"/>
/// rows in SQL; a future Azure Blob backend slots in behind the same
/// interface without changing callers.
/// </summary>
public interface IArtifactBlobStore
{
    /// <summary>
    /// Persist a bundle and return a <see cref="BlobRef"/> whose URI
    /// resolves back through <see cref="GetAsync"/>. URIs use the
    /// <c>loom-artifact://</c> scheme — see ADR-0018.
    /// </summary>
    Task<BlobRef> PutAsync(ArtifactBundle bundle, CancellationToken ct = default);

    /// <summary>
    /// Return the bundle behind <paramref name="uri"/> or null if unknown
    /// (already deleted, malformed URI, or different scheme).
    /// </summary>
    Task<ArtifactBundle?> GetAsync(string uri, CancellationToken ct = default);

    /// <summary>
    /// Drop the row backing <paramref name="uri"/>. No-op if already gone.
    /// </summary>
    Task DeleteAsync(string uri, CancellationToken ct = default);
}
