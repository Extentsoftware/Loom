using Loom.Application.Abstractions;
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
    ISystemClock clock) : IRunService
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
    }

    public async Task FailAsync(RunId runId, string reason, Cost? partialCost, CancellationToken ct = default)
    {
        var run = await GetOrThrow(runId, ct);
        run.Fail(reason, partialCost, clock.UtcNow);
        events.Add(new RunFailed(run.Id, reason, clock.UtcNow));
        await uow.SaveChangesAsync(ct);
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
