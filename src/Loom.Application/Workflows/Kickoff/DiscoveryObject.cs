using System.Text.Json.Serialization;

namespace Loom.Application.Workflows.Kickoff;

/// <summary>
/// Wire shape produced by the kickoff `discovery` step. The agent is asked
/// to fill this from the normalized transcript; the PO reviews and accepts
/// (with optional edits) at the human gate. The fields map directly onto
/// FeatureNode.ApplyDiscovery on the target node.
/// </summary>
public sealed record DiscoveryObject(
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("intent")] string? Intent,
    [property: JsonPropertyName("outcomes")] IReadOnlyList<DiscoveryOutcome> Outcomes,
    [property: JsonPropertyName("hypotheses")] IReadOnlyList<DiscoveryHypothesis> Hypotheses,
    [property: JsonPropertyName("open_questions")] IReadOnlyList<string> OpenQuestions,
    [property: JsonPropertyName("stakeholders")] IReadOnlyList<DiscoveryStakeholder> Stakeholders);

public sealed record DiscoveryOutcome(
    [property: JsonPropertyName("statement")] string Statement,
    [property: JsonPropertyName("metric_hint")] string? MetricHint,
    [property: JsonPropertyName("measurable")] bool Measurable);

public sealed record DiscoveryHypothesis(
    [property: JsonPropertyName("if")] string If,
    [property: JsonPropertyName("then")] string Then,
    [property: JsonPropertyName("because")] string Because);

public sealed record DiscoveryStakeholder(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("interest")] string? Interest);
