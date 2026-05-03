using Loom.Domain.Common;

namespace Loom.Domain.Nodes;

/// <summary>
/// The unit of context. The same entity shape applies at every level of the tree
/// (initiative ▸ feature ▸ capability ▸ slice); type is metadata.
///
/// FeatureNode is the aggregate root for its own intent, outcomes, and lifecycle.
/// Children are referenced by id, not composed in-memory: a node does not know
/// the full subtree, since trees can be deep and traversal belongs in services.
/// </summary>
public sealed class FeatureNode
{
    private readonly List<Outcome> _outcomes = [];
    private readonly List<Hypothesis> _hypotheses = [];
    private readonly List<Constraint> _constraints = [];
    private readonly List<string> _openQuestions = [];
    private readonly List<Stakeholder> _stakeholders = [];

    private FeatureNode() { } // EF Core

    private FeatureNode(
        NodeId id,
        Guid projectId,
        NodeId? parentId,
        Slug slug,
        NodeType type,
        string title,
        Guid ownerId,
        DateTimeOffset createdAt)
    {
        Id = id;
        ProjectId = projectId;
        ParentId = parentId;
        Slug = slug;
        Type = type;
        Title = title;
        OwnerId = ownerId;
        Phase = NodePhase.Discovery;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public NodeId Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public NodeId? ParentId { get; private set; }
    public Slug Slug { get; private set; }
    public NodeType Type { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Intent { get; private set; }
    public NodePhase Phase { get; private set; }
    public Guid OwnerId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public uint Version { get; private set; }

    public IReadOnlyList<Outcome> Outcomes => _outcomes.AsReadOnly();
    public IReadOnlyList<Hypothesis> Hypotheses => _hypotheses.AsReadOnly();
    public IReadOnlyList<Constraint> Constraints => _constraints.AsReadOnly();
    public IReadOnlyList<string> OpenQuestions => _openQuestions.AsReadOnly();
    public IReadOnlyList<Stakeholder> Stakeholders => _stakeholders.AsReadOnly();

    /// <summary>
    /// Creates a new node. A node always starts in the Discovery phase;
    /// other initial phases are not legal because they imply a history the
    /// node does not yet have.
    /// </summary>
    public static FeatureNode Create(
        Guid projectId,
        NodeId? parentId,
        Slug slug,
        NodeType type,
        string title,
        Guid ownerId,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (projectId == Guid.Empty)
        {
            throw new DomainException("Project id is required.");
        }
        if (ownerId == Guid.Empty)
        {
            throw new DomainException("Owner id is required.");
        }

        return new FeatureNode(NodeId.New(), projectId, parentId, slug, type, title.Trim(), ownerId, now);
    }

    /// <summary>
    /// Sets the node's intent — the one-paragraph statement of purpose
    /// that anchors all subsequent enrichment. Intent is mutable in
    /// Discovery and Enrich phases; later phases require an explicit
    /// reopening because changing intent invalidates downstream artifacts.
    /// </summary>
    public void SetIntent(string? intent, DateTimeOffset now)
    {
        if (Phase is NodePhase.Done or NodePhase.Archived)
        {
            throw new DomainException($"Cannot change intent of a {Phase} node.");
        }
        Intent = string.IsNullOrWhiteSpace(intent) ? null : intent.Trim();
        Touch(now);
    }

    public void AddOutcome(Outcome outcome, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        _outcomes.Add(outcome);
        Touch(now);
    }

    public void ReplaceOutcomes(IEnumerable<Outcome> outcomes, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(outcomes);
        _outcomes.Clear();
        _outcomes.AddRange(outcomes);
        Touch(now);
    }

    public void AddConstraint(Constraint constraint, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(constraint);
        _constraints.Add(constraint);
        Touch(now);
    }

    public void AddHypothesis(Hypothesis hypothesis, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(hypothesis);
        _hypotheses.Add(hypothesis);
        Touch(now);
    }

    public void AddOpenQuestion(string question, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        _openQuestions.Add(question.Trim());
        Touch(now);
    }

    public void AddStakeholder(Stakeholder stakeholder, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(stakeholder);
        _stakeholders.Add(stakeholder);
        Touch(now);
    }

    /// <summary>
    /// Advance the node to the next phase. Phase transitions are gated:
    /// only forward moves are allowed from a non-terminal state, and
    /// terminal states (Done, Archived) require an explicit reopen.
    /// </summary>
    public void AdvancePhase(NodePhase to, DateTimeOffset now)
    {
        if (!IsValidTransition(Phase, to))
        {
            throw new DomainException($"Invalid phase transition: {Phase} → {to}.");
        }
        Phase = to;
        Touch(now);
    }

    /// <summary>
    /// Reopen a Done or Archived node into Discovery. Used when a delivered
    /// feature is rediscovered to need work; the node retains its history but
    /// re-enters the lifecycle.
    /// </summary>
    public void Reopen(DateTimeOffset now)
    {
        if (Phase is not (NodePhase.Done or NodePhase.Archived))
        {
            throw new DomainException($"Only Done or Archived nodes can be reopened (was {Phase}).");
        }
        Phase = NodePhase.Discovery;
        Touch(now);
    }

    /// <summary>
    /// Re-parent the node. Used during PO-gate decomposition edits when
    /// restructuring the tree. Cannot make a node its own ancestor; the
    /// caller is responsible for the cycle check across the wider tree.
    /// </summary>
    public void Reparent(NodeId? newParent, DateTimeOffset now)
    {
        if (newParent.HasValue && newParent.Value == Id)
        {
            throw new DomainException("A node cannot be its own parent.");
        }
        ParentId = newParent;
        Touch(now);
    }

    public void Rename(string title, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title = title.Trim();
        Touch(now);
    }

    /// <summary>
    /// Bulk-apply the PO-accepted output of the kickoff discovery step.
    /// Atomic: all fields move together, all collections are replaced
    /// wholesale, the version number ticks once. Rejected if the node is
    /// already terminal because reopening for discovery is a deliberate
    /// state transition that should go through Reopen first.
    /// </summary>
    public void ApplyDiscovery(DiscoveryAcceptance acceptance, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(acceptance);
        if (Phase is NodePhase.Done or NodePhase.Archived)
        {
            throw new DomainException($"Cannot apply discovery to a {Phase} node; reopen first.");
        }

        if (!string.IsNullOrWhiteSpace(acceptance.Title))
        {
            Title = acceptance.Title.Trim();
        }
        Intent = string.IsNullOrWhiteSpace(acceptance.Intent) ? null : acceptance.Intent.Trim();

        _outcomes.Clear();
        _outcomes.AddRange(acceptance.Outcomes);

        _hypotheses.Clear();
        _hypotheses.AddRange(acceptance.Hypotheses);

        _openQuestions.Clear();
        foreach (var q in acceptance.OpenQuestions)
        {
            if (!string.IsNullOrWhiteSpace(q))
            {
                _openQuestions.Add(q.Trim());
            }
        }

        _stakeholders.Clear();
        _stakeholders.AddRange(acceptance.Stakeholders);

        Touch(now);
    }

    private void Touch(DateTimeOffset now)
    {
        UpdatedAt = now;
        Version++;
    }

    private static bool IsValidTransition(NodePhase from, NodePhase to)
    {
        // Allowed: Discovery → Enrich → Build → Test → Done
        // Allowed: any non-terminal phase → Archived
        // Disallowed: backward moves and skipping phases (Reopen handles re-entry)
        if (from == to)
        {
            return false;
        }
        if (to == NodePhase.Archived)
        {
            return from is not NodePhase.Archived;
        }
        return (from, to) switch
        {
            (NodePhase.Discovery, NodePhase.Enrich) => true,
            (NodePhase.Enrich, NodePhase.Build) => true,
            (NodePhase.Build, NodePhase.Test) => true,
            (NodePhase.Test, NodePhase.Done) => true,
            _ => false
        };
    }
}
