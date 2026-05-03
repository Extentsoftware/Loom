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
    public async Task Root_node_creation_skips_enrichment()
    {
        var engine = new RecordingEngine();
        var workflows = new FakeWorkflowRepository();
        var handler = new EnrichmentAutoQueueHandler(engine, workflows);

        await handler.HandleAsync(new NodeCreated(
            new NodeId(Guid.CreateVersion7()),
            ProjectId: Guid.CreateVersion7(),
            ParentId: null,
            Type: NodeType.Initiative,
            OccurredAt: DateTimeOffset.UtcNow));

        engine.Started.Should().BeEmpty();
    }

    [Fact]
    public async Task Child_node_creation_starts_enrichment_when_workflow_is_seeded()
    {
        var workflows = new FakeWorkflowRepository();
        var workflow = EnrichmentWorkflowFactory.Build(DateTimeOffset.UtcNow);
        await workflows.AddAsync(workflow);

        var engine = new RecordingEngine();
        var handler = new EnrichmentAutoQueueHandler(engine, workflows);

        var childId = new NodeId(Guid.CreateVersion7());
        await handler.HandleAsync(new NodeCreated(
            childId,
            ProjectId: Guid.CreateVersion7(),
            ParentId: new NodeId(Guid.CreateVersion7()),
            Type: NodeType.Capability,
            OccurredAt: DateTimeOffset.UtcNow));

        engine.Started.Should().ContainSingle();
        engine.Started[0].NodeId.Should().Be(childId);
        engine.Started[0].WorkflowId.Should().Be(workflow.Id);
    }

    [Fact]
    public async Task Child_node_creation_no_ops_when_workflow_missing()
    {
        var engine = new RecordingEngine();
        var handler = new EnrichmentAutoQueueHandler(engine, new FakeWorkflowRepository());

        await handler.HandleAsync(new NodeCreated(
            new NodeId(Guid.CreateVersion7()),
            Guid.CreateVersion7(),
            new NodeId(Guid.CreateVersion7()),
            NodeType.Capability,
            DateTimeOffset.UtcNow));

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
