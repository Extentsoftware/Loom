using Loom.Application.Abstractions;
using Loom.Application.Agents;
using Loom.Application.Fragments;
using Loom.Application.Runs;
using Loom.Domain.Common;
using Loom.Domain.Common.DomainEvents;
using Loom.Domain.Nodes;
using Loom.Domain.Runs;
using Loom.Domain.Workflows;

namespace Loom.Application.Workflows;

/// <summary>
/// Phase-1 workflow engine. Walks the workflow's Steps in order. State per
/// step is implicit: each step produces a Run record (Agent or HumanGate
/// kinds) or a transient string output (InProc kind). Inputs flow forward
/// keyed by step key — step "discovery" can read "normalize.output" if it
/// declares the input.
///
/// This is intentionally synchronous-looking: agent steps await the runtime
/// to completion before queueing the next step. Phase 5 swaps this for a
/// streaming/event-driven engine; the public surface stays the same.
/// </summary>
public sealed class WorkflowEngine(
    IFeatureNodeRepository nodes,
    IWorkflowRepository workflows,
    IFragmentService fragmentService,
    IAssembledPromptComposer composer,
    IRunService runService,
    IRunRepository runs,
    IAgentRouter router,
    IEnumerable<IInProcStep> inProcSteps,
    IEnumerable<IStepOutputProjector> projectors,
    IEngineHealthMonitor health,
    IDomainEventCollector events,
    ISystemClock clock) : IWorkflowEngine
{
    private readonly Dictionary<string, IInProcStep> _inProcByKey =
        inProcSteps.ToDictionary(s => s.StepKey, StringComparer.Ordinal);

    private readonly Dictionary<string, IStepOutputProjector> _projectorsBySchema =
        projectors.ToDictionary(p => p.SchemaName, StringComparer.Ordinal);

    public async Task<RunId?> StartAsync(
        NodeId nodeId,
        WorkflowId workflowId,
        IReadOnlyDictionary<string, string> initialInputs,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(initialInputs);

        var workflow = await workflows.GetAsync(workflowId, ct)
            ?? throw new DomainException($"Workflow {workflowId} not found.");
        var node = await nodes.GetAsync(nodeId, ct)
            ?? throw new DomainException($"Node {nodeId} not found.");

        var inputs = new Dictionary<string, string>(initialInputs, StringComparer.Ordinal);

        foreach (var step in workflow.Steps.OrderBy(s => s.Order))
        {
            var pausedAt = await ExecuteStepAsync(node, workflow, step, inputs, ct);
            if (pausedAt is not null)
            {
                return pausedAt;
            }
        }

        return null;
    }

    public async Task ResolveGateAsync(
        RunId runId,
        string stepKey,
        Guid resolvedBy,
        IReadOnlyDictionary<string, string>? edits = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepKey);
        var run = await runs.GetAsync(runId, ct)
            ?? throw new DomainException($"Run {runId} not found.");

        // Resume the paused run, then complete it (gate resolution closes
        // the gate run; the next step's execution kicks off below).
        await runService.ResumeAsync(runId, ct);
        await runService.AppendEventAsync(runId, seq => RunEvent.GateResolved(runId, seq, stepKey, resolvedBy, clock.UtcNow), ct);
        await runService.CompleteAsync(runId, new Cost("inproc-gate", null, 0, 0, 0m), ct);

        if (run.WorkflowId is null)
        {
            return;
        }
        var workflow = await workflows.GetAsync(new WorkflowId(run.WorkflowId.Value), ct);
        if (workflow is null)
        {
            return;
        }
        var node = await nodes.GetAsync(run.NodeId, ct);
        if (node is null)
        {
            return;
        }

        var resumedStep = workflow.FindStep(stepKey);
        if (resumedStep is null)
        {
            return;
        }

        var inputs = new Dictionary<string, string>(StringComparer.Ordinal);
        if (edits is not null)
        {
            foreach (var (k, v) in edits)
            {
                inputs[k] = v;
            }
        }

        // Continue with steps after the gate.
        foreach (var step in workflow.Steps.Where(s => s.Order > resumedStep.Order).OrderBy(s => s.Order))
        {
            var pausedAt = await ExecuteStepAsync(node, workflow, step, inputs, ct);
            if (pausedAt is not null)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Executes one step. Returns the RunId of a paused gate run if the
    /// engine has hit a human gate (caller propagates that to the UI);
    /// returns null otherwise (step completed and we should continue).
    /// </summary>
    private async Task<RunId?> ExecuteStepAsync(
        FeatureNode node,
        Workflow workflow,
        WorkflowStep step,
        Dictionary<string, string> inputs,
        CancellationToken ct)
    {
        switch (step.Kind)
        {
            case WorkflowStepKind.InProc:
                await RunInProcStepAsync(step, inputs, ct);
                return null;

            case WorkflowStepKind.Agent:
                var (output, _) = await RunAgentStepAsync(node, workflow, step, inputs, ct);
                inputs[$"{step.Key}.output"] = output;
                return step.Gating == WorkflowStepGating.Auto
                    ? null
                    : await PauseForGateAsync(node, workflow, step, output, ct);

            case WorkflowStepKind.HumanGate:
                return await PauseForGateAsync(node, workflow, step, output: string.Empty, ct);

            default:
                throw new DomainException($"Unknown WorkflowStepKind: {step.Kind}");
        }
    }

    private async Task RunInProcStepAsync(WorkflowStep step, Dictionary<string, string> inputs, CancellationToken ct)
    {
        if (!_inProcByKey.TryGetValue(step.Key, out var impl))
        {
            throw new DomainException(
                $"No IInProcStep is registered for step key '{step.Key}'. Phase 1 expects one in Loom.Agents.InProc.");
        }
        var output = await impl.ExecuteAsync(inputs, ct);
        inputs[$"{step.Key}.output"] = output;
    }

    private async Task<(string output, RunId runId)> RunAgentStepAsync(
        FeatureNode node,
        Workflow workflow,
        WorkflowStep step,
        Dictionary<string, string> inputs,
        CancellationToken ct)
    {
        if (step.EnginePref is null)
        {
            throw new DomainException($"Agent step '{step.Key}' missing EnginePref.");
        }

        // Compose prompt up-front; reused across retry attempts so the
        // assembled-prompt content (and therefore the agent's view of the
        // task) is identical regardless of which engine ends up executing.
        var fragments = await fragmentService.GetEffectiveFragmentsAsync(node.Id, step.FragmentSelectors, ct);
        var nodeContext = BuildNodeContext(node);

        var chain = BuildFallbackChain(step.EnginePref.Value);
        Exception? lastError = null;
        for (var attempt = 0; attempt < chain.Count; attempt++)
        {
            var engine = chain[attempt];
            try
            {
                var result = await TryRunOnEngineAsync(node, workflow, step, engine, fragments, nodeContext, inputs, ct);
                health.RecordSuccess(engine);
                return result;
            }
            catch (DomainException ex)
            {
                lastError = ex;
                health.RecordFailure(engine);
                // Loop to next engine in chain. The previous run is already
                // marked Failed by TryRunOnEngineAsync.
            }
        }
        throw new DomainException(
            $"Agent step '{step.Key}' failed on every engine in the fallback chain ({string.Join(", ", chain)}). Last error: {lastError?.Message}",
            lastError ?? new InvalidOperationException("no engine attempts"));
    }

    private async Task<(string output, RunId runId)> TryRunOnEngineAsync(
        FeatureNode node,
        Workflow workflow,
        WorkflowStep step,
        EngineName engine,
        IReadOnlyList<EffectiveFragment> fragments,
        NodeContext nodeContext,
        Dictionary<string, string> inputs,
        CancellationToken ct)
    {
        var runtime = router.Resolve(engine);

        var run = await runService.QueueAsync(
            node.Id,
            workflowId: workflow.Id.Value,
            stepId: step.Id.Value,
            engine: engine,
            budgets: step.Budgets,
            fragments: [],
            ct);

        var prompt = composer.Compose(step, fragments, nodeContext, inputs, run.Id, clock.UtcNow);
        await runService.AttachAssembledPromptAsync(run.Id, prompt, ct);

        await runService.AppendEventAsync(run.Id, seq => RunEvent.StepStarted(run.Id, seq, step.Key, clock.UtcNow), ct);

        var request = new AgentRunRequest(
            RunId: run.Id,
            Prompt: prompt,
            Budgets: step.Budgets,
            ToolGrants: [],
            PreferredModel: null,
            RequiresJsonOutput: !string.IsNullOrWhiteSpace(step.OutputSchemaName));

        using var inflight = health.BeginInFlight(engine);

        var externalId = await runtime.StartAsync(request, ct);
        await runService.MarkRunningAsync(run.Id, externalId, ct);

        var output = string.Empty;
        Cost? finalCost = null;
        await foreach (var evt in runtime.StreamEventsAsync(externalId, ct))
        {
            switch (evt)
            {
                case AgentRunEvent.Output o:
                    output += o.Content;
                    break;
                case AgentRunEvent.TokenUsage tokens:
                    await runService.AppendEventAsync(
                        run.Id,
                        seq => RunEvent.TokenUsage(run.Id, seq, tokens.InputTokens, tokens.OutputTokens, clock.UtcNow),
                        ct);
                    break;
                case AgentRunEvent.Completed c:
                    finalCost = c.Cost;
                    break;
                case AgentRunEvent.Failed f:
                    await runService.FailAsync(run.Id, f.Reason, f.PartialCost, ct);
                    throw new DomainException($"Agent run {run.Id} failed on {engine}: {f.Reason}");
                case AgentRunEvent.Cancelled cancelled:
                    await runService.CancelAsync(run.Id, cancelled.Reason, ct);
                    throw new DomainException($"Agent run {run.Id} cancelled on {engine}: {cancelled.Reason}");
            }
        }

        await runService.AppendEventAsync(run.Id, seq => RunEvent.StepOutput(run.Id, seq, step.Key, EncodeOutputForEvent(output), clock.UtcNow), ct);
        await runService.AppendEventAsync(run.Id, seq => RunEvent.StepCompleted(run.Id, seq, step.Key, clock.UtcNow), ct);

        if (finalCost is null)
        {
            finalCost = new Cost("unknown", null, 0, 0, 0m);
        }
        await runService.CompleteAsync(run.Id, finalCost, ct);

        if (!string.IsNullOrWhiteSpace(step.OutputSchemaName)
            && _projectorsBySchema.TryGetValue(step.OutputSchemaName, out var projector))
        {
            try
            {
                await projector.ProjectAsync(node.Id, step, run.Id, output, ct);
            }
            catch (Exception ex)
            {
                await runService.AppendEventAsync(
                    run.Id,
                    seq => RunEvent.StepOutput(run.Id, seq, step.Key, $"projection-failed: {ex.Message}", clock.UtcNow),
                    ct);
            }
        }

        return (output, run.Id);
    }

    /// <summary>
    /// Build the per-step engine fallback chain. The step's preferred engine
    /// is always tried first; the remaining hosted agent engines (excluding
    /// InProc, which isn't an agent backend) follow in declared order with
    /// duplicates removed. Capacity-aware tiebreaking happens inside the
    /// router's ResolveWithFallback; this method just decides what's *eligible*
    /// to fall back to.
    /// </summary>
    private static List<EngineName> BuildFallbackChain(EngineName preferred)
    {
        var chain = new List<EngineName> { preferred };
        foreach (var alt in new[] { EngineName.Foundry, EngineName.Anthropic })
        {
            if (!chain.Contains(alt))
            {
                chain.Add(alt);
            }
        }
        return chain;
    }

    private async Task<RunId?> PauseForGateAsync(
        FeatureNode node,
        Workflow workflow,
        WorkflowStep step,
        string output,
        CancellationToken ct)
    {
        // For human-gate-only steps (Kind=HumanGate) we still need a Run
        // record so the UI has a stable id to address. Queue + immediately
        // pause one.
        Run gateRun;
        if (step.Kind == WorkflowStepKind.HumanGate)
        {
            gateRun = await runService.QueueAsync(
                node.Id,
                workflowId: workflow.Id.Value,
                stepId: step.Id.Value,
                engine: EngineName.InProc,
                budgets: step.Budgets,
                fragments: [],
                ct);
            await runService.MarkRunningAsync(gateRun.Id, $"gate-{step.Key}", ct);
        }
        else
        {
            // The agent run that produced `output` is still in Completed
            // state; create a sibling gate run that the PO resolves.
            gateRun = await runService.QueueAsync(
                node.Id,
                workflowId: workflow.Id.Value,
                stepId: step.Id.Value,
                engine: EngineName.InProc,
                budgets: step.Budgets,
                fragments: [],
                ct);
            await runService.MarkRunningAsync(gateRun.Id, $"gate-{step.Key}", ct);
            await runService.AppendEventAsync(gateRun.Id, seq => RunEvent.StepOutput(gateRun.Id, seq, step.Key, EncodeOutputForEvent(output), clock.UtcNow), ct);
        }

        await runService.PauseForHumanAsync(gateRun.Id, ct);
        await runService.AppendEventAsync(gateRun.Id, seq => RunEvent.GatePaused(gateRun.Id, seq, step.Key, clock.UtcNow), ct);
        events.Add(new RunPausedForHuman(gateRun.Id, step.Key, MapGatingRole(step.Gating), clock.UtcNow));
        return gateRun.Id;
    }

    private static WorkflowStepGatingRole MapGatingRole(WorkflowStepGating gating) => gating switch
    {
        WorkflowStepGating.HumanPo => WorkflowStepGatingRole.Po,
        WorkflowStepGating.HumanUx => WorkflowStepGatingRole.Ux,
        WorkflowStepGating.HumanLead => WorkflowStepGatingRole.Lead,
        WorkflowStepGating.HumanDev => WorkflowStepGatingRole.Dev,
        _ => throw new DomainException($"Cannot map gating '{gating}' to a human role.")
    };

    private static NodeContext BuildNodeContext(FeatureNode node) => new(
        NodeId: node.Id,
        Title: node.Title,
        Intent: node.Intent,
        Phase: node.Phase,
        Type: node.Type,
        AncestorTitles: [],
        OpenQuestions: node.OpenQuestions,
        Outcomes: node.Outcomes,
        Hypotheses: node.Hypotheses);

    /// <summary>
    /// RunEvent.StepOutput embeds the output verbatim into a JSON payload.
    /// Strings need quoting; non-string outputs (already-JSON) pass through.
    /// </summary>
    private static string EncodeOutputForEvent(string output) =>
        System.Text.Json.JsonSerializer.Serialize(output);
}
