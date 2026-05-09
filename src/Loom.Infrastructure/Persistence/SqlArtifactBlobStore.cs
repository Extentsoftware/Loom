using Loom.Application.Abstractions;
using Loom.Application.Artifacts;
using Loom.Domain.Artifacts;
using MessagePack;
using Microsoft.EntityFrameworkCore;

namespace Loom.Infrastructure.Persistence;

/// <summary>
/// MessagePack-serialized bundles persisted into the <c>artifact_blobs</c>
/// table. Mirrors the ChatFileBundle pattern from the Marketplace MCP api
/// (single binary column, custom URI scheme). See ADR-0018.
/// </summary>
public sealed class SqlArtifactBlobStore(LoomDbContext db, ISystemClock clock) : IArtifactBlobStore
{
    public const string UriScheme = "loom-artifact";

    public async Task<BlobRef> PutAsync(ArtifactBundle bundle, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        var dto = ArtifactBundleDto.From(bundle);
        var payload = MessagePackSerializer.Serialize(dto, cancellationToken: ct);

        var id = Guid.CreateVersion7();
        var (contentType, sizeBytes) = SummarizeBundle(bundle);

        var row = new ArtifactBlob(id, payload, contentType, sizeBytes, clock.UtcNow);
        db.ArtifactBlobs.Add(row);
        await db.SaveChangesAsync(ct);

        var uri = $"{UriScheme}://{id:N}";
        return new BlobRef(uri, contentType, sizeBytes);
    }

    public async Task<ArtifactBundle?> GetAsync(string uri, CancellationToken ct = default)
    {
        if (!TryParseId(uri, out var id))
        {
            return null;
        }

        var row = await db.ArtifactBlobs.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (row is null)
        {
            return null;
        }

        var dto = MessagePackSerializer.Deserialize<ArtifactBundleDto>(row.Payload, cancellationToken: ct);
        return dto.ToBundle();
    }

    public async Task DeleteAsync(string uri, CancellationToken ct = default)
    {
        if (!TryParseId(uri, out var id))
        {
            return;
        }

        var row = await db.ArtifactBlobs.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (row is null)
        {
            return;
        }
        db.ArtifactBlobs.Remove(row);
        await db.SaveChangesAsync(ct);
    }

    private static bool TryParseId(string uri, out Guid id)
    {
        id = default;
        if (string.IsNullOrEmpty(uri))
        {
            return false;
        }
        var prefix = $"{UriScheme}://";
        if (!uri.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }
        return Guid.TryParse(uri.AsSpan(prefix.Length), out id);
    }

    /// <summary>
    /// Bundle-level content type + size used to populate the <see cref="BlobRef"/>.
    /// Single-file bundles carry that file's content-type; multi-file bundles
    /// fall back to a generic <c>application/x-loom-bundle</c> marker so
    /// callers know to enumerate the bundle rather than treat it as a single
    /// payload.
    /// </summary>
    private static (string ContentType, long SizeBytes) SummarizeBundle(ArtifactBundle bundle)
    {
        long total = 0;
        foreach (var f in bundle.Files)
        {
            total += f.Bytes.LongLength;
        }
        var contentType = bundle.Files.Count == 1
            ? bundle.Files[0].ContentType
            : "application/x-loom-bundle";
        return (contentType, total);
    }
}

[MessagePackObject]
public sealed class ArtifactBundleDto
{
    [Key(0)]
    public List<ArtifactFileDto> Files { get; set; } = [];

    public static ArtifactBundleDto From(ArtifactBundle bundle) => new()
    {
        Files = bundle.Files
            .Select(f => new ArtifactFileDto
            {
                Filename = f.Filename,
                ContentType = f.ContentType,
                Bytes = f.Bytes
            })
            .ToList()
    };

    public ArtifactBundle ToBundle() => new(
        Files.Select(f => new ArtifactFile(f.Filename, f.ContentType, f.Bytes)).ToList());
}

[MessagePackObject]
public sealed class ArtifactFileDto
{
    [Key(0)]
    public string Filename { get; set; } = string.Empty;

    [Key(1)]
    public string ContentType { get; set; } = string.Empty;

    [Key(2)]
    public byte[] Bytes { get; set; } = [];
}
