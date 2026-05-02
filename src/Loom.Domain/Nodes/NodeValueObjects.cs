namespace Loom.Domain.Nodes;

/// <summary>
/// A measurable goal the feature aims to achieve. Outcomes anchor acceptance
/// criteria and gate-level decisions; they should be falsifiable when possible.
/// </summary>
public sealed record Outcome(string Statement, string? MetricHint, bool Measurable)
{
    public static Outcome Of(string statement, string? metricHint = null, bool measurable = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statement);
        return new Outcome(statement.Trim(), metricHint?.Trim(), measurable);
    }
}

/// <summary>
/// An "if X then Y because Z" claim recorded during discovery. Hypotheses are
/// not commitments — they are the thinking behind a proposed direction, kept
/// so the team can later evaluate whether the assumption held.
/// </summary>
public sealed record Hypothesis(string If, string Then, string Because);

/// <summary>
/// A boundary the feature must respect. The kind drives downstream gating
/// (e.g. regulatory constraints invoke a compliance fragment).
/// </summary>
public sealed record Constraint(ConstraintKind Kind, string Detail);

public enum ConstraintKind
{
    Regulatory = 1,
    Technical = 2,
    Temporal = 3,
    Commercial = 4,
    Ethical = 5,
    Other = 99
}

/// <summary>
/// Pointer to a person — by user id when known, by free-text name when not.
/// Stakeholders extracted from transcripts may not yet correspond to a Loom user.
/// </summary>
public sealed record Stakeholder(Guid? UserId, string Name, string Role, string? Interest);
