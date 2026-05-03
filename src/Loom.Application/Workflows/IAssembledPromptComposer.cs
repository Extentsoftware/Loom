using Loom.Application.Fragments;
using Loom.Domain.Runs;
using Loom.Domain.Workflows;

namespace Loom.Application.Workflows;

/// <summary>
/// Composes an AssembledPrompt from a step, the effective fragments for the
/// node + step, the live node context, and step-specific inputs (transcript
/// for the kickoff normalize step, prior step outputs for downstream steps).
/// Pure: no I/O, no side effects.
/// </summary>
public interface IAssembledPromptComposer
{
    AssembledPrompt Compose(
        WorkflowStep workflowStep,
        IReadOnlyList<EffectiveFragment> fragments,
        NodeContext nodeContext,
        IReadOnlyDictionary<string, string> inputs,
        RunId runId,
        DateTimeOffset now);
}
