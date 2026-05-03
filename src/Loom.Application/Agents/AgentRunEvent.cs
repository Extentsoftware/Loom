using Loom.Domain.Runs;

namespace Loom.Application.Agents;

/// <summary>
/// Stream events produced by an IAgentRuntime as a run progresses. Modelled
/// as a sealed-record discriminated union: pattern-match on the concrete type.
/// The Application layer maps these onto domain RunEvents and the Run state
/// machine; Infrastructure stores them; SignalR broadcasts them.
/// </summary>
public abstract record AgentRunEvent
{
    private AgentRunEvent() { }

    public sealed record Started(string ExternalRunId) : AgentRunEvent;

    public sealed record Output(string Content) : AgentRunEvent;

    public sealed record TokenUsage(int InputTokens, int OutputTokens) : AgentRunEvent;

    public sealed record Completed(Cost Cost) : AgentRunEvent;

    public sealed record Failed(string Reason, Cost? PartialCost) : AgentRunEvent;

    public sealed record Cancelled(string Reason) : AgentRunEvent;
}
