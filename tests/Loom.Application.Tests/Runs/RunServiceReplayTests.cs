using FluentAssertions;
using Loom.Application.Runs;
using Loom.Application.Tests.Fakes;
using Loom.Domain.Common.DomainEvents;
using Loom.Domain.Fragments;
using Loom.Domain.Nodes;
using Loom.Domain.Runs;
using Xunit;

namespace Loom.Application.Tests.Runs;

public sealed class RunServiceReplayTests
{
    [Fact]
    public async Task ReplayAsync_clones_setup_and_annotates_source()
    {
        var clock = new FakeSystemClock();
        var runs = new FakeRunRepository();
        var runEvents = new FakeRunEventRepository();
        var prompts = new FakeAssembledPromptRepository();
        var events = new FakeDomainEventCollector();
        var uow = new FakeUnitOfWork();
        var svc = new RunService(runs, runEvents, prompts, events, uow, clock);

        var nodeId = new NodeId(Guid.CreateVersion7());
        var fragments = new List<FragmentRef>
        {
            new(new FragmentId(Guid.CreateVersion7()), new FragmentVersionId(Guid.CreateVersion7()), Version: 2)
        };
        var source = await svc.QueueAsync(
            nodeId, workflowId: Guid.CreateVersion7(), stepId: Guid.CreateVersion7(),
            engine: EngineName.Anthropic,
            budgets: new Budgets(1000, 1000, TimeSpan.FromMinutes(1), 1m),
            fragments: fragments);

        var requester = Guid.CreateVersion7();
        var fresh = await svc.ReplayAsync(source.Id, requester);

        fresh.Id.Should().NotBe(source.Id);
        fresh.NodeId.Should().Be(source.NodeId);
        fresh.WorkflowId.Should().Be(source.WorkflowId);
        fresh.StepId.Should().Be(source.StepId);
        fresh.Engine.Should().Be(source.Engine);
        fresh.State.Should().Be(RunState.Queued);
        fresh.Fragments.Should().BeEquivalentTo(source.Fragments);

        runs.ById.Should().ContainKey(fresh.Id);
        runEvents.All.Should().Contain(e => e.RunId == source.Id && e.Kind == RunEventKind.StepOutput);

        events.Recorded.OfType<RunQueued>().Should().Contain(e => e.RunId == fresh.Id);
    }
}
