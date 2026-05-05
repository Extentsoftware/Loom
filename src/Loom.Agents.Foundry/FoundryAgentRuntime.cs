using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Loom.Application.Agents;
using Loom.Domain.Runs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loom.Agents.Foundry;

/// <summary>
/// Agent runtime targeting Azure OpenAI Chat Completions in Foundry
/// (Surface A — see ADR-0017). Maps Loom's AssembledPrompt onto the
/// chat/completions wire shape, enforces budgets via a linked CTS,
/// converts FoundryStreamEvent into the application's AgentRunEvent,
/// and computes Cost from observed token usage with the deployment
/// name pinned onto Cost.Deployment.
///
/// The "external run id" is a runtime-local handle, not the Azure-side
/// chatcmpl id (we don't have that until the stream's first chunk).
/// Cancellation by external id maps onto the linked CTS for the
/// in-flight call.
/// </summary>
public sealed class FoundryAgentRuntime(
    IFoundryChatClient chatClient,
    IOptions<FoundryOptions> options,
    ILogger<FoundryAgentRuntime> logger) : IAgentRuntime
{
    private readonly ConcurrentDictionary<string, InflightRun> _inflight = new(StringComparer.Ordinal);

    public EngineName Engine => EngineName.Foundry;

    public EngineCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsToolUse: false,           // Phase 5 lights this up.
        SupportsExtendedThinking: false,  // Surface A doesn't expose a thinking knob.
        SupportsStructuredOutput: false,  // Output schema enforcement is post-decode for now.
        MaxContextTokens: 128_000);

    public Task<string> StartAsync(AgentRunRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var externalId = $"loom-foundry-{Guid.CreateVersion7():N}";
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
                $"Foundry runtime: no inflight run with id '{externalRunId}'.",
                PartialCost: null);
            yield break;
        }

        var (request, cts) = inflight;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, ct);
        if (request.Budgets.MaxWallClock is { } wall)
        {
            linked.CancelAfter(wall);
        }

        yield return new AgentRunEvent.Started(externalRunId);

        var foundryRequest = new FoundryRequest(
            SystemPrompt: request.Prompt.SystemPrompt,
            Messages: [.. request.Prompt.Messages.Select(m => new FoundryMessage(m.Role, m.Content))],
            MaxOutputTokens: request.Budgets.MaxOutputTokens,
            RequiresJsonOutput: request.RequiresJsonOutput);

        var inputTokens = 0;
        var outputTokens = 0;
        var modelUsed = string.IsNullOrEmpty(options.Value.DefaultModel)
            ? options.Value.Deployment
            : options.Value.DefaultModel;
        var failureReason = (string?)null;

        var enumerator = chatClient.StreamAsync(foundryRequest, linked.Token).GetAsyncEnumerator(linked.Token);
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
                    FoundryAgentRuntimeLog.StreamErrored(logger, externalRunId, step.Error);
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
                    case FoundryStreamEvent.Started s:
                        if (!string.IsNullOrEmpty(s.Model))
                        {
                            modelUsed = s.Model;
                        }
                        break;
                    case FoundryStreamEvent.TextDelta td:
                        yield return new AgentRunEvent.Output(td.Text);
                        break;
                    case FoundryStreamEvent.Usage u:
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
                    case FoundryStreamEvent.Stopped:
                        // Terminal completed event is emitted below with the
                        // final cost.
                        break;
                    case FoundryStreamEvent.Failed f:
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

        var cost = FoundryCostCalculator.Compute(modelUsed, options.Value.Deployment, inputTokens, outputTokens);
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

    private static async Task<StreamStep> TryAdvanceAsync(IAsyncEnumerator<FoundryStreamEvent> enumerator)
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
        public FoundryStreamEvent? Value;
        public Exception? Error;
    }

    private sealed record InflightRun(AgentRunRequest Request, CancellationTokenSource Cts);
}

internal static partial class FoundryAgentRuntimeLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Foundry stream errored for {ExternalRunId}")]
    public static partial void StreamErrored(ILogger logger, string externalRunId, Exception ex);
}
