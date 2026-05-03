using System.Runtime.CompilerServices;
using FluentAssertions;
using Loom.Agents.Anthropic;
using Loom.Application.Agents;
using Loom.Domain.Common;
using Loom.Domain.Fragments;
using Loom.Domain.Runs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Loom.Agents.Anthropic.Tests;

public sealed class AnthropicAgentRuntimeTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);

    private static AssembledPrompt SamplePrompt(RunId runId) => AssembledPrompt.Create(
        runId,
        systemPrompt: "You are a PO discovery assistant.",
        messages: [new AssembledPromptMessage("user", "<transcript>Anna: Hello.</transcript>")],
        fragments: [],
        outputSchemaJson: null,
        now: Now);

    private static AnthropicAgentRuntime MakeRuntime(IAnthropicChatClient chat, AnthropicOptions? options = null)
    {
        var opts = Options.Create(options ?? new AnthropicOptions { ApiKey = "test", DefaultModel = "claude-opus-4-7" });
        return new AnthropicAgentRuntime(chat, opts, NullLogger<AnthropicAgentRuntime>.Instance);
    }

    [Fact]
    public async Task StreamEvents_TranslatesDeltasIntoOutputAndCompletes()
    {
        var runtime = MakeRuntime(new ScriptedChatClient(
            new AnthropicStreamEvent.Started("msg_1", "claude-opus-4-7"),
            new AnthropicStreamEvent.Usage(InputTokens: 1500, OutputTokens: 0),
            new AnthropicStreamEvent.TextDelta("Hello "),
            new AnthropicStreamEvent.TextDelta("world."),
            new AnthropicStreamEvent.Usage(InputTokens: 1500, OutputTokens: 600),
            new AnthropicStreamEvent.Stopped("end_turn")));

        var runId = RunId.New();
        var request = new AgentRunRequest(
            RunId: runId,
            Prompt: SamplePrompt(runId),
            Budgets: new Budgets(MaxInputTokens: null, MaxOutputTokens: 4096, MaxWallClock: null, MaxCostUsd: null),
            ToolGrants: [],
            PreferredModel: null);

        var externalId = await runtime.StartAsync(request);
        var events = await ToList(runtime.StreamEventsAsync(externalId));

        events.Should().NotBeEmpty();
        events.First().Should().BeOfType<AgentRunEvent.Started>();
        events.OfType<AgentRunEvent.Output>().Select(e => e.Content)
            .Should().Equal("Hello ", "world.");
        events.OfType<AgentRunEvent.TokenUsage>().Should().NotBeEmpty();
        events.Last().Should().BeOfType<AgentRunEvent.Completed>();

        var completed = (AgentRunEvent.Completed)events.Last();
        completed.Cost.InputTokens.Should().Be(1500);
        completed.Cost.OutputTokens.Should().Be(600);
        completed.Cost.UsdAmount.Should().BeGreaterThan(0m);
        completed.Cost.Model.Should().Be("claude-opus-4-7");
    }

    [Fact]
    public async Task StreamEvents_TranslatesFailureIntoFailed()
    {
        var runtime = MakeRuntime(new ScriptedChatClient(
            new AnthropicStreamEvent.Failed("HTTP 429: rate limited")));

        var runId = RunId.New();
        var externalId = await runtime.StartAsync(new AgentRunRequest(
            runId, SamplePrompt(runId),
            new Budgets(null, null, null, null),
            ToolGrants: [], PreferredModel: null));

        var events = await ToList(runtime.StreamEventsAsync(externalId));

        events.Last().Should().BeOfType<AgentRunEvent.Failed>()
            .Which.Reason.Should().Contain("rate limited");
    }

    [Fact]
    public async Task StreamEvents_HonorsCancelAsync()
    {
        var slow = new SlowChatClient(TimeSpan.FromSeconds(30));
        var runtime = MakeRuntime(slow);

        var runId = RunId.New();
        var externalId = await runtime.StartAsync(new AgentRunRequest(
            runId, SamplePrompt(runId),
            new Budgets(null, null, null, null),
            [], null));

        var collect = ToList(runtime.StreamEventsAsync(externalId));
        await Task.Delay(50);
        await runtime.CancelAsync(externalId);

        var events = await collect;
        events.Last().Should().BeOfType<AgentRunEvent.Cancelled>();
    }

    [Fact]
    public void Capabilities_ExposesPhaseOneSurface()
    {
        var runtime = MakeRuntime(new ScriptedChatClient());
        runtime.Engine.Should().Be(EngineName.Anthropic);
        runtime.Capabilities.SupportsStreaming.Should().BeTrue();
        runtime.Capabilities.SupportsToolUse.Should().BeFalse();
        runtime.Capabilities.MaxContextTokens.Should().BeGreaterThan(100_000);
    }

    private static async Task<List<AgentRunEvent>> ToList(IAsyncEnumerable<AgentRunEvent> source)
    {
        var list = new List<AgentRunEvent>();
        await foreach (var e in source)
        {
            list.Add(e);
        }
        return list;
    }

    private sealed class ScriptedChatClient(params AnthropicStreamEvent[] script) : IAnthropicChatClient
    {
        public async IAsyncEnumerable<AnthropicStreamEvent> StreamAsync(
            AnthropicRequest request,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var e in script)
            {
                ct.ThrowIfCancellationRequested();
                yield return e;
                await Task.Yield();
            }
        }
    }

    private sealed class SlowChatClient(TimeSpan delayBeforeYield) : IAnthropicChatClient
    {
        public async IAsyncEnumerable<AnthropicStreamEvent> StreamAsync(
            AnthropicRequest request,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.Delay(delayBeforeYield, ct);
            yield return new AnthropicStreamEvent.Stopped("end_turn");
        }
    }
}
