using System.Text.Json.Serialization;

namespace Loom.Application.Workflows.Kickoff;

/// <summary>
/// Decides whether a kickoff transcript should run as a single-feature
/// workflow (Feature root, one decompose into capabilities) or as a
/// multi-feature initiative (Initiative root, decompose returns a tree
/// of features each with their own capabilities).
///
/// The result is advisory only — the PO sees the classification on the
/// Kickoff page and can override it before starting the workflow.
/// </summary>
public interface IScopeClassifier
{
    Task<ScopeClassification> ClassifyAsync(string transcript, CancellationToken ct = default);
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "'Single' here is a kickoff scope, not the System.Single type — naming reads naturally in UI and prompts.")]
public enum KickoffScope
{
    Single,
    Multi
}

/// <summary>
/// Classifier output. <see cref="Confidence"/> is 0..1 — the Kickoff
/// page uses anything above 0.6 as a strong-enough signal to pre-select
/// the recommended mode; below that, it shows both options without a
/// pre-selection.
/// </summary>
public sealed record ScopeClassification(
    [property: JsonPropertyName("mode")] KickoffScope Mode,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("suggestedTitles")] IReadOnlyList<string> SuggestedTitles);
