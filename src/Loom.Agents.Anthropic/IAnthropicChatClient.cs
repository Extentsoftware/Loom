namespace Loom.Agents.Anthropic;

/// <summary>
/// SDK-agnostic seam for the Anthropic chat surface. The default implementation
/// (AnthropicChatClient) wraps the Anthropic.SDK community NuGet; swapping to
/// raw HttpClient or a different SDK is a one-class change.
/// </summary>
public interface IAnthropicChatClient
{
    IAsyncEnumerable<AnthropicStreamEvent> StreamAsync(
        AnthropicRequest request,
        CancellationToken ct = default);
}

/// <summary>
/// Request sent to the Anthropic Messages API. Phase 1: system prompt, a
/// linear list of user/assistant messages, model + budget knobs. Tool use,
/// extended thinking, and prompt caching arrive in later phases.
/// </summary>
public sealed record AnthropicRequest(
    string Model,
    string SystemPrompt,
    IReadOnlyList<AnthropicMessage> Messages,
    int? MaxOutputTokens,
    bool ExtendedThinking);

public sealed record AnthropicMessage(string Role, string Content);

/// <summary>
/// Stream events produced as the model responds. Loom.Agents.Anthropic
/// translates these into the application-layer AgentRunEvent shape.
/// </summary>
public abstract record AnthropicStreamEvent
{
    private AnthropicStreamEvent() { }

    public sealed record Started(string MessageId, string Model) : AnthropicStreamEvent;

    public sealed record TextDelta(string Text) : AnthropicStreamEvent;

    public sealed record Usage(int InputTokens, int OutputTokens) : AnthropicStreamEvent;

    public sealed record Stopped(string StopReason) : AnthropicStreamEvent;

    public sealed record Failed(string Reason) : AnthropicStreamEvent;
}
