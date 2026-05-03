using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Loom.Application.Agents;
using Loom.Domain.Runs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loom.Agents.Anthropic;

/// <summary>
/// Phase-1 agent runtime targeting the Anthropic Messages API. Maps the
/// application's AssembledPrompt onto the Anthropic wire shape, enforces
/// budgets via a linked CTS, converts AnthropicStreamEvent into the
/// application's AgentRunEvent, and computes Cost from observed token usage.
///
/// The "external run id" Loom records is a runtime-local handle, not the
/// engine's message id (Anthropic doesn't expose a stable id ahead of the
/// stream's first message_start event). Cancellation by external id maps
/// onto the linked CTS for the in-flight call.
/// </summary>
public sealed class AnthropicAgentRuntime(
    IAnthropicChatClient chatClient,
    IOptions<AnthropicOptions> options,
    ILogger<AnthropicAgentRuntime> logger) : IAgentRuntime
{
    private readonly ConcurrentDictionary<string, InflightRun> _inflight = new(StringComparer.Ordinal);

    public EngineName Engine => EngineName.Anthropic;

    public EngineCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsToolUse: false,           // Phase 5 lights this up.
        SupportsExtendedThinking: true,
        SupportsStructuredOutput: false,  // Output schema enforcement is post-decode for now.
        MaxContextTokens: 200_000);

    public Task<string> StartAsync(AgentRunRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Loom's external run id is opaque to the caller — they pass it back
        // to StreamEventsAsync / CancelAsync. We mint one up-front so the
        // caller can record provenance before we've made the network call.
        var externalId = $"loom-anth-{Guid.CreateVersion7():N}";
        _inflight[externalId] = new InflightRun(request, new CancellationTokenSource());
        return Task.FromResult(externalId);
    }

    public Task CancelAsync(string externalRunId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalRunId);
        if (_inflight.TryGetValue(externalRunId, out var inflight))
        {
            inflight.Cts.Cancel();
        }
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<AgentRunEvent> StreamEventsAsync(
        string externalRunId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalRunId);
        if (!_inflight.TryGetValue(externalRunId, out var inflight))
        {
            yield return new AgentRunEvent.Failed(
                $"Anthropic runtime: no inflight run with id '{externalRunId}'.",
                PartialCost: null);
            yield break;
        }

        var (request, cts) = inflight;

        // Link the runtime's CTS (cancelled by Loom) with the caller's
        // observation token so either side can stop the stream.
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, ct);
        // Wall-clock budget guard.
        if (request.Budgets.MaxWallClock is { } wall)
        {
            linked.CancelAfter(wall);
        }

        yield return new AgentRunEvent.Started(externalRunId);

        var anthropicRequest = new AnthropicRequest(
            Model: request.PreferredModel ?? options.Value.DefaultModel,
            SystemPrompt: request.Prompt.SystemPrompt,
            Messages: [.. request.Prompt.Messages.Select(m => new AnthropicMessage(m.Role, m.Content))],
            MaxOutputTokens: request.Budgets.MaxOutputTokens,
            ExtendedThinking: options.Value.ExtendedThinking);

        var inputTokens = 0;
        var outputTokens = 0;
        var modelUsed = anthropicRequest.Model;
        var failureReason = (string?)null;

        var enumerator = chatClient.StreamAsync(anthropicRequest, linked.Token).GetAsyncEnumerator(linked.Token);
        var cancelled = false;
        try
        {
            while (true)
            {
                var step = await TryAdvanceAsync(enumerator);
                if (step.Cancelled)
                {
                    cancelled = true;
                    break;
                }
                if (step.Error is not null)
                {
                    AnthropicAgentRuntimeLog.StreamErrored(logger, externalRunId, step.Error);
                    failureReason = step.Error.Message;
                    break;
                }
                if (!step.HasValue)
                {
                    break;
                }
                var evt = step.Value;

                switch (evt)
                {
                    case AnthropicStreamEvent.Started s:
                        if (!string.IsNullOrEmpty(s.Model))
                        {
                            modelUsed = s.Model;
                        }
                        break;
                    case AnthropicStreamEvent.TextDelta td:
                        yield return new AgentRunEvent.Output(td.Text);
                        break;
                    case AnthropicStreamEvent.Usage u:
                        // Anthropic emits usage twice: once on message_start
                        // (input only) and once on message_delta (final).
                        if (u.InputTokens > inputTokens)
                        {
                            inputTokens = u.InputTokens;
                        }
                        if (u.OutputTokens > outputTokens)
                        {
                            outputTokens = u.OutputTokens;
                        }
                        yield return new AgentRunEvent.TokenUsage(inputTokens, outputTokens);
                        break;
                    case AnthropicStreamEvent.Stopped:
                        // The terminal completed event is emitted below
                        // outside the loop with the final cost.
                        break;
                    case AnthropicStreamEvent.Failed f:
                        failureReason = f.Reason;
                        break;
                }
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
            if (_inflight.TryRemove(externalRunId, out var removed))
            {
                removed.Cts.Dispose();
            }
        }

        var cost = AnthropicCostCalculator.Compute(modelUsed, inputTokens, outputTokens);
        if (cancelled)
        {
            yield return new AgentRunEvent.Cancelled("cancelled");
        }
        else if (failureReason is not null)
        {
            yield return new AgentRunEvent.Failed(failureReason, cost);
        }
        else
        {
            yield return new AgentRunEvent.Completed(cost);
        }
    }

    private static async Task<StreamStep> TryAdvanceAsync(IAsyncEnumerator<AnthropicStreamEvent> enumerator)
    {
        try
        {
            return await enumerator.MoveNextAsync()
                ? new StreamStep { HasValue = true, Value = enumerator.Current }
                : new StreamStep { HasValue = false };
        }
        catch (OperationCanceledException)
        {
            return new StreamStep { Cancelled = true };
        }
        catch (HttpRequestException ex)
        {
            return new StreamStep { Error = ex };
        }
        catch (IOException ex)
        {
            return new StreamStep { Error = ex };
        }
    }

    private struct StreamStep
    {
        public bool HasValue;
        public bool Cancelled;
        public AnthropicStreamEvent? Value;
        public Exception? Error;
    }

    private sealed record InflightRun(AgentRunRequest Request, CancellationTokenSource Cts);
}

internal static partial class AnthropicAgentRuntimeLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Anthropic stream errored for {ExternalRunId}")]
    public static partial void StreamErrored(ILogger logger, string externalRunId, Exception ex);
}
