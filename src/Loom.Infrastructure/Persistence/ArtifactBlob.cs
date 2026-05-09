namespace Loom.Infrastructure.Persistence;

/// <summary>
/// Storage row backing the <c>loom-artifact://{Id}</c> URI scheme. Holds a
/// MessagePack-serialized <c>ArtifactBundle</c> as a byte payload. Lives in
/// infrastructure (not domain) because it is a storage detail of the blob
/// store seam — see ADR-0018.
/// </summary>
internal sealed class ArtifactBlob
{
    private ArtifactBlob() { } // EF Core

    public ArtifactBlob(Guid id, byte[] payload, string contentType, long sizeBytes, DateTimeOffset createdAt)
    {
        Id = id;
        Payload = payload;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public byte[] Payload { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
