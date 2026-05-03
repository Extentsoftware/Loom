using System.Text.Json.Serialization;
using Loom.Domain.Nodes;

namespace Loom.Application.Workflows.Kickoff;

/// <summary>
/// Wire shape produced by the kickoff `decompose` step. The agent proposes a
/// list of child nodes (per the v1 default: stop at capabilities). The PO
/// reviews each and accepts/rejects/edits at the second human gate; accepted
/// proposals turn into real FeatureNodes via FeatureService.CreateChildNodeAsync.
/// </summary>
public sealed record DecompositionProposal(
    [property: JsonPropertyName("children")] IReadOnlyList<ProposedChild> Children);

public sealed record ProposedChild(
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("intent")] string? Intent)
{
    public NodeType ParseType() => Type?.Trim().ToLowerInvariant() switch
    {
        "initiative" => NodeType.Initiative,
        "feature" => NodeType.Feature,
        "capability" => NodeType.Capability,
        "slice" => NodeType.Slice,
        _ => NodeType.Capability   // safe default — Phase-1 stops at capabilities
    };
}
