using System.Runtime.CompilerServices;
using System.Text.Json;
using FluentAssertions;
using Loom.Agents.Anthropic;
using Loom.Agents.InProc;
using Loom.Application.Abstractions;
using Loom.Application.Agents;
using Loom.Application.Common;
using Loom.Application.Features;
using Loom.Application.Fragments;
using Loom.Application.Runs;
using Loom.Application.Tests.Fakes;
using Loom.Application.Workflows;
using Loom.Application.Workflows.Kickoff;
using Loom.Domain.Common;
using Loom.Domain.Nodes;
using Loom.Domain.Runs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Loom.Application.Tests.Integration;

/// <summary>
/// Phase-1 acceptance test. Exercises the kickoff path through the *real*
/// application services (FeatureService / FragmentService / RunService /
/// WorkflowEngine / AssembledPromptComposer / AnthropicAgentRuntime) with
/// in-memory fake repositories and a stubbed Anthropic chat client.
///
/// EF Core round-trips are covered separately by Loom.Infrastructure.Tests
/// against Testcontainers MSSQL — this test deliberately avoids the
/// relational provider so it runs every PR without Docker.
///
/// The flow it asserts mirrors the demo path:
///   1. Workflow is published (kickoff/v1).
///   2. PO posts a transcript.
///   3. Engine runs normalize → discovery → pauses at gate (HumanPo).
///   4. PO accepts the (canned) DiscoveryObject onto the target node.
///   5. Engine continues to decompose → pauses at second gate.
///   6. PO accepts and creates child nodes.
///   7. Engine completes the workflow.
/// </summary>
public sealed class EndToEndKickoffTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid PoUserId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    [Fact]
    public async Task Kickoff_WalksNormalizeDiscoveryDecompose_ToCompletion()
    {
        // ── Arrange: fakes + real services ───────────────────────────
        var projects = new FakeProjectRepository();
        var nodes = new FakeFeatureNodeRepository();
        var fragments = new FakeFragmentRepository();
        var runs = new FakeRunRepository();
        var runEvents = new FakeRunEventRepository();
        var assembled = new FakeAssembledPromptRepository();
        var workflows = new FakeWorkflowRepository();
        var events = new FakeDomainEventCollector();
        var uow = new FakeUnitOfWork();
        var clock = new FakeSystemClock(Now);

        var fragmentService = new FragmentService(fragments, nodes, uow, clock);
        var runService = new RunService(runs, runEvents, assembled, events, uow, clock);
        var composer = new AssembledPromptComposer();
        var features = new FeatureService(projects, nodes, runs, events, uow, clock);

        // Real Anthropic runtime over a stubbed chat client.
        var stubbedChat = new StubbedAnthropicChatClient();
        var anthropic = new AnthropicAgentRuntime(
            stubbedChat,
            Options.Create(new AnthropicOptions { ApiKey = "stub", DefaultModel = "claude-opus-4-7" }),
            NullLogger<AnthropicAgentRuntime>.Instance);
        var router = new DefaultAgentRouter([anthropic]);

        var inProcSteps = new IInProcStep[] { new TranscriptNormalizeStep() };

        var engine = new WorkflowEngine(
            nodes, workflows, fragmentService, composer, runService,
            runs, router, inProcSteps, events, clock);

        // Seed the kickoff workflow + a project + the placeholder root node.
        var workflow = KickoffWorkflowFactory.Build(Now);
        await workflows.AddAsync(workflow);

        var project = await features.CreateProjectAsync(Slug.From("acme"), "Acme", "Test project");
        var rootNode = await features.CreateRootNodeAsync(
            project.Id, Slug.From("checkout"), NodeType.Feature, "Untitled", PoUserId);

        // ── Act 1: kick off → pause at discovery gate ────────────────
        var firstGate = await engine.StartAsync(rootNode.Id, workflow.Id,
            new Dictionary<string, string> { ["transcript"] = "Anna: We need express checkout." });

        firstGate.Should().NotBeNull("the engine should pause at the discovery gate");
        var discoveryRun = runs.ById[firstGate!.Value];
        discoveryRun.State.Should().Be(RunState.PausedForHuman);
        events.Recorded.OfType<Loom.Domain.Common.DomainEvents.RunPausedForHuman>()
            .Should().HaveCount(1);

        // The discovery run's StepOutput should carry the agent's
        // DiscoveryObject JSON.
        var discoveryOutput = runEvents.All
            .Where(e => e.RunId == discoveryRun.Id && e.Kind == RunEventKind.StepOutput)
            .Should().ContainSingle().Which;
        discoveryOutput.PayloadJson.Should().Contain("Express checkout");

        // ── Act 2: PO accepts the discovery ──────────────────────────
        var acceptance = new DiscoveryAcceptance(
            Title: "Express checkout",
            Intent: "Reduce friction at the payment step.",
            Outcomes: [Outcome.Of("Cart-to-purchase conversion +5%", "%", measurable: true)],
            Hypotheses: [],
            OpenQuestions: ["EU coverage of saved cards?"],
            Stakeholders: []);

        await features.ApplyDiscoveryAsync(rootNode.Id, acceptance, PoUserId);

        // Resolve the gate — engine should advance to the decompose step
        // and pause at *that* gate.
        await engine.ResolveGateAsync(
            discoveryRun.Id,
            KickoffWorkflowFactory.DiscoveryStepKey,
            PoUserId,
            edits: new Dictionary<string, string>
            {
                [$"{KickoffWorkflowFactory.DiscoveryStepKey}.output"] = JsonSerializer.Serialize(acceptance)
            });

        // The discovery acceptance should be visible on the node.
        var refreshed = (await nodes.GetAsync(rootNode.Id))!;
        refreshed.Title.Should().Be("Express checkout");
        refreshed.Intent.Should().StartWith("Reduce friction");
        refreshed.Outcomes.Should().ContainSingle();

        // The decompose run should be paused at its gate.
        var decomposeRun = runs.ById.Values
            .Where(r => r.NodeId == rootNode.Id && r.State == RunState.PausedForHuman && r.Id != discoveryRun.Id)
            .Should().ContainSingle().Which;

        // ── Act 3: PO accepts the decomposition ──────────────────────
        // The stubbed Anthropic client returned a DecompositionProposal;
        // mirror what the PoGate page does — create the accepted children
        // and resolve the gate.
        await features.CreateChildNodeAsync(
            rootNode.Id, Slug.From("guest-checkout"), NodeType.Capability,
            "Guest checkout path", PoUserId);
        await features.CreateChildNodeAsync(
            rootNode.Id, Slug.From("saved-card"), NodeType.Capability,
            "Saved card surfacing", PoUserId);

        await engine.ResolveGateAsync(
            decomposeRun.Id, KickoffWorkflowFactory.DecomposeStepKey, PoUserId);

        // ── Assert ───────────────────────────────────────────────────
        var children = await nodes.GetChildrenAsync(rootNode.Id);
        children.Select(c => c.Slug.Value).Should().BeEquivalentTo(["guest-checkout", "saved-card"]);

        runs.ById[decomposeRun.Id].State.Should().Be(RunState.Completed);

        // Cost was recorded on the discovery run from the stubbed Anthropic
        // usage events.
        runs.ById.Values
            .Where(r => r.Engine == EngineName.Anthropic && r.State == RunState.Completed)
            .Should().HaveCountGreaterThanOrEqualTo(2, "both agent runs should have completed cost-tracked");
    }

    /// <summary>
    /// Stub IAnthropicChatClient that returns canned text: a DiscoveryObject
    /// for the first call (the discovery step), a DecompositionProposal for
    /// the second (the decompose step). The runtime aggregates TextDelta
    /// events so the engine sees `output = body` at completion.
    /// </summary>
    private sealed class StubbedAnthropicChatClient : IAnthropicChatClient
    {
        private int _calls;

        public async IAsyncEnumerable<AnthropicStreamEvent> StreamAsync(
            AnthropicRequest request,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var call = Interlocked.Increment(ref _calls);
            yield return new AnthropicStreamEvent.Started($"stub-{call}", request.Model);

            string body = call == 1 ? DiscoveryJson : DecompositionJson;
            yield return new AnthropicStreamEvent.TextDelta(body);
            yield return new AnthropicStreamEvent.Usage(InputTokens: 100, OutputTokens: 50);
            yield return new AnthropicStreamEvent.Stopped("end_turn");
            await Task.CompletedTask;
        }

        private const string DiscoveryJson = """
            {"title":"Express checkout","intent":"Reduce friction.","outcomes":[],"hypotheses":[],"open_questions":[],"stakeholders":[]}
            """;

        private const string DecompositionJson = """
            {"children":[{"slug":"guest-checkout","title":"Guest checkout path","type":"Capability","intent":null}]}
            """;
    }
}
