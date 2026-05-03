namespace Loom.Domain.Workflows;

/// <summary>
/// What a step actually does. Agent steps invoke an IAgentRuntime through
/// the router; HumanGate steps pause the run and notify the assigned role;
/// InProc steps invoke a registered IInProcStep keyed by step key (used for
/// cheap, deterministic transformations like transcript normalisation).
/// </summary>
public enum WorkflowStepKind
{
    Agent = 1,
    HumanGate = 2,
    InProc = 3
}

/// <summary>
/// Whether a step proceeds automatically or requires human acceptance.
/// Phase 1 uses Auto and HumanPo; later phases add HumanUx, HumanLead.
/// </summary>
public enum WorkflowStepGating
{
    Auto = 1,
    HumanPo = 2,
    HumanUx = 3,
    HumanLead = 4
}
