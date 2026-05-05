using Loom.Application.Abstractions;
using Loom.Application.Workflows;
using Loom.Domain.Common;
using Loom.Domain.Common.DomainEvents;
using Loom.Domain.Runs;
using Loom.Domain.Workflows;

namespace Loom.Application.Runs;

public sealed class RunAssignmentService(
    IRunRepository runs,
    IFeatureNodeRepository nodes,
    IWorkflowRepository workflows,
    IAssembledPromptRepository prompts,
    IWorkflowEngine engine,
    IDomainEventCollector events,
    ISystemClock clock,
    IUnitOfWork uow) : IRunAssignmentService
{
    public async Task<IReadOnlyList<TaskEnvelope>> ListClaimableAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("UserId must be a non-empty GUID.");
        }
        var matching = await runs.ListClaimableForAsync(userId, ct);
        return await MaterializeAsync(matching, ct);
    }

    public async Task<IReadOnlyList<TaskEnvelope>> ListAssignedAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("UserId must be a non-empty GUID.");
        }
        var matching = (await runs.ListClaimableForAsync(userId, ct))
            .Where(r => r.AssigneeUserId == userId)
            .ToList();
        return await MaterializeAsync(matching, ct);
    }

    public async Task<TaskEnvelope> ClaimAsync(RunId runId, Guid userId, CancellationToken ct = default)
    {
        var run = await runs.GetAsync(runId, ct)
            ?? throw new DomainException($"Run {runId} not found.");
        run.ClaimBy(userId, clock.UtcNow);
        events.Add(new RunAssigned(run.Id, userId, ViaClaim: true, clock.UtcNow));
        await uow.SaveChangesAsync(ct);

        var single = await MaterializeAsync(new[] { run }, ct);
        return single[0];
    }

    public async Task ReleaseAsync(RunId runId, Guid userId, CancellationToken ct = default)
    {
        var run = await runs.GetAsync(runId, ct)
            ?? throw new DomainException($"Run {runId} not found.");
        if (run.AssigneeUserId.HasValue && run.AssigneeUserId.Value != userId)
        {
            throw new DomainException(
                $"Run {runId} is assigned to a different user; cannot release.");
        }
        var prev = run.AssigneeUserId;
        run.Release(clock.UtcNow);
        if (prev.HasValue)
        {
            events.Add(new RunReleased(run.Id, prev.Value, clock.UtcNow));
        }
        await uow.SaveChangesAsync(ct);
    }

    public async Task CompleteAsync(RunId runId, Guid userId, IReadOnlyDictionary<string, string>? edits, CancellationToken ct = default)
    {
        var run = await runs.GetAsync(runId, ct)
            ?? throw new DomainException($"Run {runId} not found.");
        if (run.State is not RunState.PausedForHuman)
        {
            throw new DomainException(
                $"Run {runId} is not paused for a human gate (state: {run.State}).");
        }
        if (run.AssigneeUserId is null || run.AssigneeUserId.Value != userId)
        {
            throw new DomainException(
                $"Run {runId} is not assigned to you. Claim it first.");
        }
        if (run.WorkflowId is null || run.StepId is null)
        {
            throw new DomainException("Run has no workflow/step linkage; cannot complete.");
        }

        var workflow = await workflows.GetAsync(new WorkflowId(run.WorkflowId.Value), ct)
            ?? throw new DomainException("Workflow for this run is missing.");
        var step = workflow.Steps.FirstOrDefault(s => s.Id.Value == run.StepId.Value)
            ?? throw new DomainException("Step for this run is missing.");

        // ResolveGateAsync handles the state-machine + advance-or-complete
        // semantics; we just bridge the assignment-checked call to it.
        await engine.ResolveGateAsync(run.Id, step.Key, userId, edits, ct);
    }

    private async Task<IReadOnlyList<TaskEnvelope>> MaterializeAsync(IEnumerable<Run> source, CancellationToken ct)
    {
        var list = new List<TaskEnvelope>();
        foreach (var r in source)
        {
            var node = await nodes.GetAsync(r.NodeId, ct);
            string? stepKey = null;
            string? schema = null;
            if (r.WorkflowId.HasValue && r.StepId.HasValue)
            {
                var wf = await workflows.GetAsync(new WorkflowId(r.WorkflowId.Value), ct);
                var step = wf?.Steps.FirstOrDefault(s => s.Id.Value == r.StepId.Value);
                stepKey = step?.Key;
                schema = step?.OutputSchemaName;
            }
            string? promptText = null;
            if (r.AssembledPromptId.HasValue)
            {
                var prompt = await prompts.GetByRunAsync(r.Id, ct);
                promptText = prompt is null
                    ? null
                    : string.Join("\n\n", prompt.Messages.Select(m => $"[{m.Role}] {m.Content}"));
            }
            list.Add(new TaskEnvelope(
                RunId: r.Id,
                NodeId: r.NodeId,
                NodeTitle: node?.Title ?? r.NodeId.Value.ToString("N"),
                StepKey: stepKey,
                OutputSchemaName: schema,
                PromptText: promptText,
                AssigneeUserId: r.AssigneeUserId,
                CreatedAt: r.CreatedAt,
                DeepLink: $"/runs/{r.Id.Value:D}/gate"));
        }
        return list;
    }
}
