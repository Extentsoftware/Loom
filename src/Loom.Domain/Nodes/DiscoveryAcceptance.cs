namespace Loom.Domain.Nodes;

/// <summary>
/// The PO-edited discovery output that gets bulk-applied to a FeatureNode at
/// the kickoff PO gate. Carries the values the human approved (potentially
/// edited from what the agent produced); the agent's raw output is preserved
/// separately on the Run for provenance.
/// </summary>
public sealed record DiscoveryAcceptance(
    string? Title,
    string? Intent,
    IReadOnlyList<Outcome> Outcomes,
    IReadOnlyList<Hypothesis> Hypotheses,
    IReadOnlyList<string> OpenQuestions,
    IReadOnlyList<Stakeholder> Stakeholders);
