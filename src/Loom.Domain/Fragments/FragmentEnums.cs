namespace Loom.Domain.Fragments;

public enum FragmentCategory
{
    /// <summary>Who the agent is acting as ("PO discovery assistant", "frontend dev").</summary>
    Identity = 1,
    /// <summary>How this team thinks (problem framing, definition of ready/done).</summary>
    Methodology = 2,
    /// <summary>Stack conventions, repo layout, design tokens, naming.</summary>
    Project = 3,
    /// <summary>Business glossary, personas, regulatory rules.</summary>
    Domain = 4,
    /// <summary>Discrete capabilities ("extract acceptance criteria", "write ADR").</summary>
    Skill = 5,
    /// <summary>Live node content, auto-injected, not authored by humans.</summary>
    Context = 6,
    /// <summary>Annotations from validation gates, auto-derived from human input.</summary>
    Feedback = 7
}

public enum FragmentScope
{
    /// <summary>Applies to all projects.</summary>
    Global = 1,
    /// <summary>Applies within one project.</summary>
    Project = 2,
    /// <summary>Applies to one node and its descendants.</summary>
    Node = 3
}
