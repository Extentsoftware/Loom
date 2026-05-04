namespace Loom.Agents.Foundry;

/// <summary>
/// SDK-agnostic seam for the Azure OpenAI Chat Completions surface.
/// The default implementation (FoundryChatClient) wraps HttpClient + STJ;
/// swapping to Azure.AI.OpenAI or another SDK is a one-class change.
/// </summary>
public interface IFoundryChatClient
{
    IAsyncEnumerable<FoundryStreamEvent> StreamAsync(
        FoundryRequest request,
        CancellationToken ct = default);
}

/// <summary>
/// Request sent to Azure OpenAI's chat/completions endpoint. The
/// deployment is part of the URL (set on FoundryOptions); the model
/// field is intentionally omitted — Azure derives the model from the
/// deployment.
/// </summary>
public sealed record FoundryRequest(
    string SystemPrompt,
    IReadOnlyList<FoundryMessage> Messages,
    int? MaxOutputTokens);

public sealed record FoundryMessage(string Role, string Content);

/// <summary>
/// Stream events produced as the model responds. Loom.Agents.Foundry
/// translates these into the application-layer AgentRunEvent shape.
///
/// Mirrors the AnthropicStreamEvent discriminated union 1:1 so the
/// FoundryAgentRuntime translator can match the Anthropic runtime's
/// shape line-for-line.
/// </summary>
public abstract record FoundryStreamEvent
{
    private FoundryStreamEvent() { }

    public sealed record Started(string Id, string Model) : FoundryStreamEvent;

    public sealed record TextDelta(string Text) : FoundryStreamEvent;

    public sealed record Usage(int InputTokens, int OutputTokens) : FoundryStreamEvent;

    public sealed record Stopped(string FinishReason) : FoundryStreamEvent;

    public sealed record Failed(string Reason) : FoundryStreamEvent;
}
