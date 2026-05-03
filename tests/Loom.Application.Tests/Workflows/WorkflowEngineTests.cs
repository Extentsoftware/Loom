using System.Runtime.CompilerServices;
using FluentAssertions;
using Loom.Application.Agents;
using Loom.Application.Fragments;
using Loom.Application.Runs;
using Loom.Application.Tests.Fakes;
using Loom.Application.Workflows;
using Loom.Domain.Common;
using Loom.Domain.Common.DomainEvents;
using Loom.Domain.Fragments;
using Loom.Domain.Nodes;
using Loom.Domain.Runs;
using Loom.Domain.Workflows;
using Xunit;

namespace Loom.Application.Tests.Workflows;

public sealed class WorkflowEngineTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Owner = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task StartAsync_RunsInProcThenAgent_PausesAtHumanGate()
    {
        // Arrange ─────────────────────────────────────────────────────
        var nodes = new FakeFeatureNodeRepository();
        var workflows = new FakeWorkflowRepository();
        var fragments = new FakeFragmentRepository();
        var runs = new FakeRunRepository();
        var runEvents = new FakeRunEventRepository();
        var assembledPrompts = new FakeAssembledPromptRepository();
        var events = new FakeDomainEventCollector();
        var uow = new FakeUnitOfWork();
        var clock = new FakeSystemClock(Now);

        var node = FeatureNode.Create(Guid.NewGuid(), parentId: null, Slug.From("kickoff-target"), NodeType.Feature, "Untitled", Owner, Now);
        await nodes.AddAsync(node);

        // Seed a global identity fragment so the discovery step has
        // something to compose.
        var identity = Fragment.Create(Slug.From("po-discovery-assistant"), FragmentCategory.Identity, FragmentScope.Global, null, "PO discovery", Owner, Now);
        identity.PublishVersion("you are a PO discovery assistant", new EngineHints(), null, Owner, Now);
        fragments.ById[identity.Id] = identity;

        var defaultBudgets = new Budgets(MaxInputTokens: null, MaxOutputTokens: null, MaxWallClock: null, MaxCostUsd: null);
        var workflow = Workflow.Create(
            Slug.From("kickoff"), 1, "Kickoff",
            [
                new WorkflowStepDraft("normalize", WorkflowStepKind.InProc, WorkflowStepGating.Auto,
                    EnginePref: null, OutputSchemaName: null, Budgets: defaultBudgets, Selectors: []),
                new WorkflowStepDraft("discovery", WorkflowStepKind.Agent, WorkflowStepGating.HumanPo,
                    EnginePref: EngineName.Anthropic, OutputSchemaName: "DiscoveryObject", Budgets: defaultBudgets,
                    Selectors: [new FragmentSelector(FragmentCategory.Identity, Slug.From("po-discovery-assistant"))])
            ],
            Now);
        await workflows.AddAsync(workflow);

        var fragmentService = new FragmentService(fragments, nodes, uow, clock);
        var composer = new AssembledPromptComposer();
        var runService = new RunService(runs, runEvents, assembledPrompts, events, uow, clock);

        var fakeRuntime = new FakeAgentRuntime("""{"intent":"Reduce checkout friction"}""");
        var router = new DefaultAgentRouter([fakeRuntime]);

        var inProcSteps = new IInProcStep[]
        {
            new EchoNormalizeStep()
        };

        var engine = new WorkflowEngine(
            nodes, workflows, fragmentService, composer, runService, runs,
            router, inProcSteps, events, clock);

        // Act ─────────────────────────────────────────────────────────
        var pausedAt = await engine.StartAsync(
            node.Id, workflow.Id,
            new Dictionary<string, string> { ["transcript"] = "Anna: We need express checkout." });

        // Assert ──────────────────────────────────────────────────────
        pausedAt.Should().NotBeNull();
        var pausedRun = runs.ById[pausedAt!.Value];
        pausedRun.State.Should().Be(RunState.PausedForHuman);

        // The discovery agent run should have completed before the gate paused.
        var agentRuns = runs.ById.Values.Where(r => r.Engine == EngineName.Anthropic).ToList();
        agentRuns.Should().ContainSingle();
        agentRuns[0].State.Should().Be(RunState.Completed);

        // RunPausedForHuman event should have been emitted.
        events.Recorded.OfType<RunPausedForHuman>().Should().ContainSingle();

        // Acting on the gate continues the workflow (no further steps after
        // discovery in this test, so nothing more to assert).
        await engine.ResolveGateAsync(pausedAt.Value, "discovery", Guid.NewGuid());
        runs.ById[pausedAt.Value].State.Should().Be(RunState.Completed);
    }

    private sealed class EchoNormalizeStep : IInProcStep
    {
        public string StepKey => "normalize";
        public Task<string> ExecuteAsync(IReadOnlyDictionary<string, string> inputs, CancellationToken ct = default)
        {
            inputs.TryGetValue("transcript", out var t);
            return Task.FromResult($"[normalized] {t}");
        }
    }

    private sealed class FakeAgentRuntime(string output) : IAgentRuntime
    {
        public EngineName Engine => EngineName.Anthropic;
        public EngineCapabilities Capabilities => new(true, false, false, true, MaxContextTokens: 200_000);

        public Task<string> StartAsync(AgentRunRequest request, CancellationToken ct = default) =>
            Task.FromResult($"fake-{Guid.NewGuid():N}");

        public Task CancelAsync(string externalRunId, CancellationToken ct = default) => Task.CompletedTask;

        public async IAsyncEnumerable<AgentRunEvent> StreamEventsAsync(
            string externalRunId, [EnumeratorCancellation] CancellationToken ct = default)
        {
            yield return new AgentRunEvent.Started(externalRunId);
            yield return new AgentRunEvent.Output(output);
            yield return new AgentRunEvent.TokenUsage(InputTokens: 100, OutputTokens: 50);
            yield return new AgentRunEvent.Completed(new Cost("fake-model", null, 100, 50, 0.001m));
            await Task.CompletedTask;
        }
    }
}
