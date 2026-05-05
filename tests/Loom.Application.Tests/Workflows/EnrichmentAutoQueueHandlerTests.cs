using FluentAssertions;
using Loom.Application.Tests.Fakes;
using Loom.Application.Workflows;
using Loom.Application.Workflows.Enrichment;
using Loom.Domain.Common.DomainEvents;
using Loom.Domain.Nodes;
using Loom.Domain.Runs;
using Loom.Domain.Workflows;
using Xunit;

namespace Loom.Application.Tests.Workflows;

public sealed class EnrichmentAutoQueueHandlerTests
{
    [Fact]
    public async Task Empty_child_list_skips_enrichment()
    {
        var engine = new RecordingEngine();
        var workflows = new FakeWorkflowRepository();
        var handler = new EnrichmentAutoQueueHandler(engine, workflows);

        await handler.HandleAsync(new KickoffDecomposeAccepted(
            ParentNodeId: new NodeId(Guid.CreateVersion7()),
            ChildNodeIds: [],
            AcceptedBy: Guid.CreateVersion7(),
            OccurredAt: DateTimeOffset.UtcNow));

        engine.Started.Should().BeEmpty();
    }

    [Fact]
    public async Task Each_accepted_child_starts_enrichment_when_workflow_is_seeded()
    {
        var workflows = new FakeWorkflowRepository();
        var workflow = EnrichmentWorkflowFactory.Build(DateTimeOffset.UtcNow);
        await workflows.AddAsync(workflow);

        var engine = new RecordingEngine();
        var handler = new EnrichmentAutoQueueHandler(engine, workflows);

        var childA = new NodeId(Guid.CreateVersion7());
        var childB = new NodeId(Guid.CreateVersion7());
        await handler.HandleAsync(new KickoffDecomposeAccepted(
            ParentNodeId: new NodeId(Guid.CreateVersion7()),
            ChildNodeIds: [childA, childB],
            AcceptedBy: Guid.CreateVersion7(),
            OccurredAt: DateTimeOffset.UtcNow));

        engine.Started.Should().HaveCount(2);
        engine.Started.Select(s => s.NodeId).Should().BeEquivalentTo(new[] { childA, childB });
        engine.Started.Should().OnlyContain(s => s.WorkflowId == workflow.Id);
    }

    [Fact]
    public async Task No_ops_when_enrichment_workflow_missing()
    {
        var engine = new RecordingEngine();
        var handler = new EnrichmentAutoQueueHandler(engine, new FakeWorkflowRepository());

        await handler.HandleAsync(new KickoffDecomposeAccepted(
            ParentNodeId: new NodeId(Guid.CreateVersion7()),
            ChildNodeIds: [new NodeId(Guid.CreateVersion7())],
            AcceptedBy: Guid.CreateVersion7(),
            OccurredAt: DateTimeOffset.UtcNow));

        engine.Started.Should().BeEmpty();
    }

    private sealed class RecordingEngine : IWorkflowEngine
    {
        public List<(NodeId NodeId, WorkflowId WorkflowId)> Started { get; } = [];
        public Task<RunId?> StartAsync(NodeId nodeId, WorkflowId workflowId, IReadOnlyDictionary<string, string> initialInputs, CancellationToken ct = default)
        {
            Started.Add((nodeId, workflowId));
            return Task.FromResult<RunId?>(null);
        }
        public Task ResolveGateAsync(RunId runId, string stepKey, Guid resolvedBy, IReadOnlyDictionary<string, string>? edits = null, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
