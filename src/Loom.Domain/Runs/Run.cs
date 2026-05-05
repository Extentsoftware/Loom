using Loom.Domain.Common;
using Loom.Domain.Nodes;

namespace Loom.Domain.Runs;

/// <summary>
/// A single agent execution against a node. The Run record is the durable
/// provenance object: it captures who started it, which fragments were
/// composed, which tools were granted, what the engine produced, and the
/// resulting cost. All AI-driven work in Loom flows through a Run.
///
/// The Run aggregate is deliberately strict about its state machine to make
/// engine integrations easier to reason about. Webhooks and engine adapters
/// must funnel updates through MarkRunning, PauseForHuman, etc.
/// </summary>
public sealed class Run
{
    private readonly List<FragmentRef> _fragments = [];

    private Run() { }

    private Run(
        RunId id,
        NodeId nodeId,
        Guid? workflowId,
        Guid? stepId,
        EngineName engine,
        Budgets budgets,
        DateTimeOffset createdAt)
    {
        Id = id;
        NodeId = nodeId;
        WorkflowId = workflowId;
        StepId = stepId;
        Engine = engine;
        State = RunState.Queued;
        Budgets = budgets;
        CreatedAt = createdAt;
    }

    public RunId Id { get; private set; }
    public NodeId NodeId { get; private set; }
    public Guid? WorkflowId { get; private set; }
    public Guid? StepId { get; private set; }
    public EngineName Engine { get; private set; }
    public string? ExternalRunId { get; private set; }
    public RunState State { get; private set; }
    public Budgets Budgets { get; private set; } = null!;
    public Cost? Cost { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public AssembledPromptId? AssembledPromptId { get; private set; }

    /// <summary>
    /// Optional assignee — the user (Loom user id) responsible for human-
    /// gated work on this run. Set either explicitly via <see cref="AssignTo"/>
    /// (e.g. by an orchestrator) or pulled by a developer's MCP client via
    /// <see cref="ClaimBy"/>. Null means the run is unassigned and any
    /// matching user is free to claim it.
    /// </summary>
    public Guid? AssigneeUserId { get; private set; }
    public DateTimeOffset? AssignedAt { get; private set; }

    public IReadOnlyList<FragmentRef> Fragments => _fragments.AsReadOnly();

    public static Run Queue(
        NodeId nodeId,
        Guid? workflowId,
        Guid? stepId,
        EngineName engine,
        Budgets budgets,
        IEnumerable<FragmentRef> fragments,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(budgets);
        ArgumentNullException.ThrowIfNull(fragments);

        var run = new Run(RunId.New(), nodeId, workflowId, stepId, engine, budgets, now);
        run._fragments.AddRange(fragments);
        return run;
    }

    /// <summary>
    /// Pin the assembled prompt that was sent to the engine for this run.
    /// Only legal while the run is still in Queued state — the prompt is part
    /// of the run's provenance and cannot be re-assigned mid-flight.
    /// </summary>
    public void AttachAssembledPrompt(AssembledPromptId promptId)
    {
        if (State is not RunState.Queued)
        {
            throw new DomainException($"Cannot attach an assembled prompt to a {State} run.");
        }
        if (AssembledPromptId.HasValue)
        {
            throw new DomainException("This run already has an assembled prompt attached.");
        }
        AssembledPromptId = promptId;
    }

    public void MarkRunning(string externalRunId, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalRunId);
        Require(State is RunState.Queued, RunState.Running);
        State = RunState.Running;
        ExternalRunId = externalRunId;
        StartedAt = now;
    }

    public void PauseForHuman(DateTimeOffset _)
    {
        Require(State is RunState.Running, RunState.PausedForHuman);
        State = RunState.PausedForHuman;
    }

    public void Resume(DateTimeOffset _)
    {
        Require(State is RunState.PausedForHuman, RunState.Running);
        State = RunState.Running;
    }

    public void Complete(Cost cost, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(cost);
        Require(State is RunState.Running, RunState.Completed);
        State = RunState.Completed;
        Cost = cost;
        CompletedAt = now;
    }

    public void Fail(string reason, Cost? cost, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Require(State is RunState.Queued or RunState.Running or RunState.PausedForHuman, RunState.Failed);
        State = RunState.Failed;
        FailureReason = reason.Trim();
        Cost = cost;
        CompletedAt = now;
    }

    public void Cancel(string reason, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Require(State is RunState.Queued or RunState.Running or RunState.PausedForHuman, RunState.Cancelled);
        State = RunState.Cancelled;
        FailureReason = reason.Trim();
        CompletedAt = now;
    }

    /// <summary>
    /// Assign this run to a user. Legal at any non-terminal state — an
    /// orchestrator may pre-assign before queuing, or reassign during a
    /// pause. Throws on terminal runs.
    /// </summary>
    public void AssignTo(Guid userId, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("AssigneeUserId must be a non-empty GUID.");
        }
        if (IsTerminal)
        {
            throw new DomainException($"Cannot assign a {State} run.");
        }
        AssigneeUserId = userId;
        AssignedAt = now;
    }

    /// <summary>
    /// Pull-style assignment — succeeds only if the run is currently
    /// unassigned. Used by the MCP claim path so two developers' Claude
    /// instances can't both grab the same task.
    /// </summary>
    public void ClaimBy(Guid userId, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("AssigneeUserId must be a non-empty GUID.");
        }
        if (IsTerminal)
        {
            throw new DomainException($"Cannot claim a {State} run.");
        }
        if (AssigneeUserId.HasValue && AssigneeUserId.Value != userId)
        {
            throw new DomainException(
                $"Run {Id} is already claimed by {AssigneeUserId.Value}.");
        }
        AssigneeUserId = userId;
        AssignedAt = now;
    }

    /// <summary>
    /// Drop the current assignee. No-op if already unassigned.
    /// </summary>
    public void Release(DateTimeOffset _)
    {
        AssigneeUserId = null;
        AssignedAt = null;
    }

    public bool IsTerminal =>
        State is RunState.Completed or RunState.Failed or RunState.Cancelled;

    private void Require(bool condition, RunState attempted)
    {
        if (!condition)
        {
            throw new DomainException($"Invalid run state transition: {State} → {attempted}.");
        }
    }
}
