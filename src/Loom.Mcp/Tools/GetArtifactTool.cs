using System.ComponentModel;
using System.Text.Json.Serialization;
using Loom.Application.Artifacts;
using ModelContextProtocol.Server;

namespace Loom.Mcp.Tools;

/// <summary>
/// MCP tool: <c>loom_get_artifact</c>. Resolves a <c>loom-artifact://{id}</c>
/// URI to the underlying file bundle so agents can fetch screenshots,
/// design exports, and other binary seed material on demand. See ADR-0018.
/// Bytes are returned base64-encoded — most current MCP clients can't carry
/// raw binary payloads, and base64 round-trips through every transport.
/// </summary>
[McpServerToolType]
public static class GetArtifactTool
{
    [McpServerTool(Name = "loom_get_artifact")]
    [Description("Fetch the file bundle behind a loom-artifact:// URI. Returns each file's filename, content_type, and base64-encoded bytes.")]
    public static async Task<GetArtifactResult> GetAsync(
        IArtifactBlobStore blobStore,
        [Description("The loom-artifact:// URI to resolve. Obtain these from loom_list_project_artifacts.")] string uri,
        CancellationToken ct = default)
    {
        var bundle = await blobStore.GetAsync(uri, ct);
        if (bundle is null)
        {
            return new GetArtifactResult(uri, Found: false, Files: []);
        }

        var files = bundle.Files
            .Select(f => new ArtifactFileDto(
                Filename: f.Filename,
                ContentType: f.ContentType,
                SizeBytes: f.Bytes.LongLength,
                Base64: Convert.ToBase64String(f.Bytes)))
            .ToList();

        return new GetArtifactResult(uri, Found: true, Files: files);
    }

    public sealed record GetArtifactResult(
        [property: JsonPropertyName("uri")] string Uri,
        [property: JsonPropertyName("found")] bool Found,
        [property: JsonPropertyName("files")] IReadOnlyList<ArtifactFileDto> Files);

    public sealed record ArtifactFileDto(
        [property: JsonPropertyName("filename")] string Filename,
        [property: JsonPropertyName("content_type")] string ContentType,
        [property: JsonPropertyName("size_bytes")] long SizeBytes,
        [property: JsonPropertyName("base64")] string Base64);
}
