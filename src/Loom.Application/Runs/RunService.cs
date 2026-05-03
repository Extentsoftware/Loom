using Loom.Application.Abstractions;
using Loom.Application.BudgetControl;
using Loom.Domain.Common;
using Loom.Domain.Common.DomainEvents;
using Loom.Domain.Nodes;
using Loom.Domain.Runs;

namespace Loom.Application.Runs;

public sealed class RunService(
    IRunRepository runs,
    IRunEventRepository runEvents,
    IAssembledPromptRepository assembledPrompts,
    IDomainEventCollector events,
    IUnitOfWork uow,
    ISystemClock clock,
    IFeatureNodeRepository? featureNodes = null,
    IBudgetService? budgetService = null) : IRunService
{
    public async Task<Run> QueueAsync(
        NodeId nodeId,
        Guid? workflowId,
        Guid? stepId,
        EngineName engine,
        Budgets budgets,
        IReadOnlyList<FragmentRef> fragments,
        CancellationToken ct = default)
    {
        // Phase-5 budget gate: if a project budget service is wired and we
        // can resolve the node's project, refuse to queue when the
        // circuit breaker is open. Use the per-run cap as the reservation
        // estimate (no per-call cost forecasting yet).
        if (budgetService is not null && featureNodes is not null)
        {
            var node = await featureNodes.GetAsync(nodeId, ct);
            if (node is not null)
            {
                var estimate = budgets.MaxCostUsd ?? 0m;
                var allowed = await budgetService.TryReserveAsync(node.ProjectId, estimate, ct);
                if (!allowed)
                {
                    throw new DomainException(
                        $"Project {node.ProjectId} has hit its daily $-cap; refusing to queue run.");
                }
            }
        }

        var run = Run.Queue(nodeId, workflowId, stepId, engine, budgets, fragments, clock.UtcNow);
        await runs.AddAsync(run, ct);
        events.Add(new RunQueued(run.Id, nodeId, workflowId, stepId, engine, clock.UtcNow));
        await uow.SaveChangesAsync(ct);
        return run;
    }

    public async Task AttachAssembledPromptAsync(RunId runId, AssembledPrompt prompt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        var run = await GetOrThrow(runId, ct);
        await assembledPrompts.AddAsync(prompt, ct);
        run.AttachAssembledPrompt(prompt.Id);
        await uow.SaveChangesAsync(ct);
    }

    public async Task MarkRunningAsync(RunId runId, string externalRunId, CancellationToken ct = default)
    {
        var run = await GetOrThrow(runId, ct);
        run.MarkRunning(externalRunId, clock.UtcNow);
        events.Add(new RunStarted(run.Id, externalRunId, clock.UtcNow));
        await uow.SaveChangesAsync(ct);
    }

    public async Task RecordEventAsync(RunId runId, RunEvent runEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(runEvent);
        await runEvents.AddAsync(runEvent, ct);
        await uow.SaveChangesAsync(ct);
    }

    public async Task<RunEvent> AppendEventAsync(
        RunId runId,
        Func<int, RunEvent> factory,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(factory);
        var sequence = await runEvents.GetNextSequenceAsync(runId, ct);
        var evt = factory(sequence);
        await runEvents.AddAsync(evt, ct);
        await uow.SaveChangesAsync(ct);
        return evt;
    }

    public async Task CompleteAsync(RunId runId, Cost cost, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(cost);
        var run = await GetOrThrow(runId, ct);
        run.Complete(cost, clock.UtcNow);
        events.Add(new RunCompleted(run.Id, cost.UsdAmount, clock.UtcNow));
        await uow.SaveChangesAsync(ct);
        await RecordSpendAsync(run, cost.UsdAmount, ct);
    }

    public async Task FailAsync(RunId runId, string reason, Cost? partialCost, CancellationToken ct = default)
    {
        var run = await GetOrThrow(runId, ct);
        run.Fail(reason, partialCost, clock.UtcNow);
        events.Add(new RunFailed(run.Id, reason, clock.UtcNow));
        await uow.SaveChangesAsync(ct);
        if (partialCost is not null)
        {
            await RecordSpendAsync(run, partialCost.UsdAmount, ct);
        }
    }

    private async Task RecordSpendAsync(Run run, decimal usd, CancellationToken ct)
    {
        if (budgetService is null || featureNodes is null || usd <= 0m)
        {
            return;
        }
        var node = await featureNodes.GetAsync(run.NodeId, ct);
        if (node is null)
        {
            return;
        }
        await budgetService.RecordSpendAsync(node.ProjectId, usd, ct);
    }

    public async Task PauseForHumanAsync(RunId runId, CancellationToken ct = default)
    {
        var run = await GetOrThrow(runId, ct);
        run.PauseForHuman(clock.UtcNow);
        // RunPausedForHuman is emitted by the WorkflowEngine which knows the
        // step key + role; the RunService level only knows the run.
        await uow.SaveChangesAsync(ct);
    }

    public async Task ResumeAsync(RunId runId, CancellationToken ct = default)
    {
        var run = await GetOrThrow(runId, ct);
        run.Resume(clock.UtcNow);
        await uow.SaveChangesAsync(ct);
    }

    public async Task CancelAsync(RunId runId, string reason, CancellationToken ct = default)
    {
        var run = await GetOrThrow(runId, ct);
        run.Cancel(reason, clock.UtcNow);
        events.Add(new RunCancelled(run.Id, reason, clock.UtcNow));
        await uow.SaveChangesAsync(ct);
    }

    public async Task<Run> ReplayAsync(RunId sourceRunId, Guid requestedBy, CancellationToken ct = default)
    {
        var source = await GetOrThrow(sourceRunId, ct);
        var fresh = Run.Queue(
            nodeId: source.NodeId,
            workflowId: source.WorkflowId,
            stepId: source.StepId,
            engine: source.Engine,
            budgets: source.Budgets,
            fragments: source.Fragments.ToList(),
            now: clock.UtcNow);
        await runs.AddAsync(fresh, ct);

        // Annotate the source run with a ReplayQueued event so Run Detail
        // can render "replayed as <new-run-id>". Append on the source's
        // existing sequence space.
        var sourceSeq = await runEvents.GetNextSequenceAsync(source.Id, ct);
        var payload = $"{{\"replayed_as\":\"{fresh.Id.Value:D}\",\"by\":\"{requestedBy:N}\"}}";
        await runEvents.AddAsync(
            RunEvent.StepOutput(source.Id, sourceSeq, "replay", System.Text.Json.JsonSerializer.Serialize(payload), clock.UtcNow),
            ct);

        events.Add(new RunQueued(fresh.Id, source.NodeId, source.WorkflowId, source.StepId, source.Engine, clock.UtcNow));
        await uow.SaveChangesAsync(ct);
        return fresh;
    }

    private async Task<Run> GetOrThrow(RunId runId, CancellationToken ct)
    {
        return await runs.GetAsync(runId, ct)
            ?? throw new DomainException($"Run {runId} not found.");
    }
}
