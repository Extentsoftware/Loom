using Loom.Application.Abstractions;
using Loom.Domain.Artifacts;
using Loom.Domain.Common;
using Loom.Domain.Fragments;
using Loom.Domain.Integrations;
using Loom.Domain.Nodes;
using Loom.Domain.Runs;
using Loom.Domain.Workflows;
using Microsoft.EntityFrameworkCore;

namespace Loom.Infrastructure.Persistence;

public sealed class FeatureNodeRepository(LoomDbContext db) : IFeatureNodeRepository
{
    public Task<FeatureNode?> GetAsync(NodeId id, CancellationToken ct = default) =>
        db.Nodes.FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task<IReadOnlyList<FeatureNode>> GetChildrenAsync(NodeId parentId, CancellationToken ct = default) =>
        await db.Nodes.Where(n => n.ParentId == parentId).OrderBy(n => n.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<FeatureNode>> GetRootsAsync(Guid projectId, CancellationToken ct = default) =>
        await db.Nodes
            .Where(n => n.ProjectId == projectId && n.ParentId == null)
            .OrderBy(n => n.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<FeatureNode>> GetByProjectAsync(Guid projectId, CancellationToken ct = default) =>
        await db.Nodes
            .Where(n => n.ProjectId == projectId)
            .OrderBy(n => n.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<FeatureNode>> SearchAsync(string query, Guid? projectId, int take, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }
        var like = $"%{query.Trim()}%";
        var q = db.Nodes.AsQueryable();
        if (projectId is Guid pid)
        {
            q = q.Where(n => n.ProjectId == pid);
        }
        return await q
            .Where(n => EF.Functions.Like(n.Title, like) || EF.Functions.Like(n.Intent!, like))
            .OrderByDescending(n => n.UpdatedAt)
            .Take(take <= 0 ? 25 : take)
            .ToListAsync(ct);
    }

    public Task AddAsync(FeatureNode node, CancellationToken ct = default)
    {
        db.Nodes.Add(node);
        return Task.CompletedTask;
    }

    public void Remove(FeatureNode node) => db.Nodes.Remove(node);
}

public sealed class ProjectRepository(LoomDbContext db) : IProjectRepository
{
    public Task<Project?> GetAsync(Guid id, CancellationToken ct = default) =>
        db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Project?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var s = Domain.Common.Slug.From(slug);
        return db.Projects.FirstOrDefaultAsync(p => p.Slug == s, ct);
    }

    public async Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct = default) =>
        await db.Projects.OrderBy(p => p.Name).ToListAsync(ct);

    public Task AddAsync(Project project, CancellationToken ct = default)
    {
        db.Projects.Add(project);
        return Task.CompletedTask;
    }
}

public sealed class FragmentRepository(LoomDbContext db) : IFragmentRepository
{
    public Task<Fragment?> GetAsync(FragmentId id, CancellationToken ct = default) =>
        db.Fragments.FirstOrDefaultAsync(f => f.Id == id, ct);

    public Task<Fragment?> GetByKeyAsync(string key, FragmentScope scope, Guid? scopeId, CancellationToken ct = default)
    {
        var k = Domain.Common.Slug.From(key);
        return db.Fragments.FirstOrDefaultAsync(
            f => f.Key == k && f.Scope == scope && f.ScopeId == scopeId, ct);
    }

    public async Task<IReadOnlyList<Fragment>> ListByCategoryAsync(FragmentCategory category, CancellationToken ct = default) =>
        await db.Fragments.Where(f => f.Category == category).OrderBy(f => f.Title).ToListAsync(ct);

    public async Task<IReadOnlyList<Fragment>> ListGlobalAsync(CancellationToken ct = default) =>
        await db.Fragments.Where(f => f.Scope == FragmentScope.Global).OrderBy(f => f.Title).ToListAsync(ct);

    public async Task<IReadOnlyList<Fragment>> ListByProjectAsync(Guid projectId, CancellationToken ct = default) =>
        await db.Fragments
            .Where(f => f.Scope == FragmentScope.Project && f.ScopeId == projectId)
            .OrderBy(f => f.Title)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Fragment>> ListByNodeAsync(Guid nodeId, CancellationToken ct = default) =>
        await db.Fragments
            .Where(f => f.Scope == FragmentScope.Node && f.ScopeId == nodeId)
            .OrderBy(f => f.Title)
            .ToListAsync(ct);

    public Task AddAsync(Fragment fragment, CancellationToken ct = default)
    {
        db.Fragments.Add(fragment);
        return Task.CompletedTask;
    }
}

public sealed class RunRepository(LoomDbContext db) : IRunRepository
{
    public Task<Run?> GetAsync(RunId id, CancellationToken ct = default) =>
        db.Runs.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<Run>> GetByNodeAsync(NodeId nodeId, CancellationToken ct = default) =>
        await db.Runs.Where(r => r.NodeId == nodeId).OrderByDescending(r => r.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<Run>> GetActiveAsync(CancellationToken ct = default) =>
        await db.Runs
            .Where(r => r.State == RunState.Queued || r.State == RunState.Running || r.State == RunState.PausedForHuman)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);

    public Task AddAsync(Run run, CancellationToken ct = default)
    {
        db.Runs.Add(run);
        return Task.CompletedTask;
    }
}

public sealed class ArtifactRepository(LoomDbContext db) : IArtifactRepository
{
    public Task<Artifact?> GetAsync(ArtifactId id, CancellationToken ct = default) =>
        db.Artifacts.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Artifact>> GetByNodeAsync(NodeId nodeId, CancellationToken ct = default) =>
        await db.Artifacts.Where(a => a.NodeId == nodeId).OrderByDescending(a => a.UpdatedAt).ToListAsync(ct);

    public Task AddAsync(Artifact artifact, CancellationToken ct = default)
    {
        db.Artifacts.Add(artifact);
        return Task.CompletedTask;
    }
}

public sealed class WorkflowRepository(LoomDbContext db) : IWorkflowRepository
{
    public Task<Workflow?> GetAsync(WorkflowId id, CancellationToken ct = default) =>
        db.Workflows.FirstOrDefaultAsync(w => w.Id == id, ct);

    public Task<Workflow?> GetByKeyAsync(Slug key, int version, CancellationToken ct = default) =>
        db.Workflows.FirstOrDefaultAsync(w => w.Key == key && w.Version == version, ct);

    public async Task<Workflow?> GetCurrentByKeyAsync(Slug key, CancellationToken ct = default) =>
        await db.Workflows
            .Where(w => w.Key == key)
            .OrderByDescending(w => w.Version)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Workflow>> ListAsync(CancellationToken ct = default) =>
        await db.Workflows.OrderBy(w => w.Key).ThenByDescending(w => w.Version).ToListAsync(ct);

    public Task AddAsync(Workflow workflow, CancellationToken ct = default)
    {
        db.Workflows.Add(workflow);
        return Task.CompletedTask;
    }
}

public sealed class RunEventRepository(LoomDbContext db) : IRunEventRepository
{
    public async Task<IReadOnlyList<RunEvent>> GetByRunAsync(RunId runId, CancellationToken ct = default) =>
        await db.RunEvents
            .Where(e => e.RunId == runId)
            .OrderBy(e => e.Sequence)
            .ToListAsync(ct);

    public async Task<int> GetNextSequenceAsync(RunId runId, CancellationToken ct = default)
    {
        var max = await db.RunEvents
            .Where(e => e.RunId == runId)
            .Select(e => (int?)e.Sequence)
            .MaxAsync(ct);
        return (max ?? -1) + 1;
    }

    public Task AddAsync(RunEvent runEvent, CancellationToken ct = default)
    {
        db.RunEvents.Add(runEvent);
        return Task.CompletedTask;
    }
}

public sealed class AssembledPromptRepository(LoomDbContext db) : IAssembledPromptRepository
{
    public Task<AssembledPrompt?> GetAsync(AssembledPromptId id, CancellationToken ct = default) =>
        db.AssembledPrompts.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<AssembledPrompt?> GetByRunAsync(RunId runId, CancellationToken ct = default) =>
        db.AssembledPrompts.FirstOrDefaultAsync(p => p.RunId == runId, ct);

    public Task AddAsync(AssembledPrompt prompt, CancellationToken ct = default)
    {
        db.AssembledPrompts.Add(prompt);
        return Task.CompletedTask;
    }
}

public sealed class TranscriptRepository(LoomDbContext db) : ITranscriptRepository
{
    public Task<Transcript?> GetAsync(TranscriptId id, CancellationToken ct = default) =>
        db.Transcripts.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<Transcript?> GetByRunAsync(RunId runId, CancellationToken ct = default) =>
        db.Transcripts.FirstOrDefaultAsync(t => t.RunId == runId, ct);

    public Task AddAsync(Transcript transcript, CancellationToken ct = default)
    {
        db.Transcripts.Add(transcript);
        return Task.CompletedTask;
    }
}

public sealed class IntegrationConnectionRepository(LoomDbContext db) : IIntegrationConnectionRepository
{
    public Task<IntegrationConnection?> GetAsync(IntegrationConnectionId id, CancellationToken ct = default) =>
        db.IntegrationConnections.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<IntegrationConnection>> ListAsync(CancellationToken ct = default) =>
        await db.IntegrationConnections.OrderBy(c => c.Kind).ThenBy(c => c.DisplayName).ToListAsync(ct);

    public async Task<IReadOnlyList<IntegrationConnection>> ListByKindAsync(IntegrationKind kind, CancellationToken ct = default) =>
        await db.IntegrationConnections.Where(c => c.Kind == kind).ToListAsync(ct);

    public async Task<IReadOnlyList<IntegrationConnection>> ListByProjectAsync(Guid? projectId, CancellationToken ct = default) =>
        await db.IntegrationConnections.Where(c => c.ProjectId == projectId).ToListAsync(ct);

    public Task AddAsync(IntegrationConnection connection, CancellationToken ct = default)
    {
        db.IntegrationConnections.Add(connection);
        return Task.CompletedTask;
    }
}

public sealed class WebhookDeliveryRepository(LoomDbContext db) : IWebhookDeliveryRepository
{
    public Task<WebhookDelivery?> GetAsync(WebhookDeliveryId id, CancellationToken ct = default) =>
        db.WebhookDeliveries.FirstOrDefaultAsync(w => w.Id == id, ct);

    public async Task<IReadOnlyList<WebhookDelivery>> GetByConnectionAsync(IntegrationConnectionId connectionId, int take, CancellationToken ct = default) =>
        await db.WebhookDeliveries
            .Where(w => w.ConnectionId == connectionId)
            .OrderByDescending(w => w.ReceivedAt)
            .Take(take)
            .ToListAsync(ct);

    public Task AddAsync(WebhookDelivery delivery, CancellationToken ct = default)
    {
        db.WebhookDeliveries.Add(delivery);
        return Task.CompletedTask;
    }
}

public sealed class ConversationRepository(LoomDbContext db) : IConversationRepository
{
    public Task<Loom.Domain.Conversations.Conversation?> GetAsync(Loom.Domain.Conversations.ConversationId id, CancellationToken ct = default) =>
        db.Conversations.Include("_messages").FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Loom.Domain.Conversations.Conversation?> GetByRunAsync(RunId runId, CancellationToken ct = default) =>
        db.Conversations.Include("_messages").FirstOrDefaultAsync(c => c.RunId == runId, ct);

    public async Task<IReadOnlyList<Loom.Domain.Conversations.Conversation>> GetByNodeAsync(NodeId nodeId, CancellationToken ct = default) =>
        await db.Conversations.Include("_messages").Where(c => c.NodeId == nodeId).OrderByDescending(c => c.UpdatedAt).ToListAsync(ct);

    public Task AddAsync(Loom.Domain.Conversations.Conversation conversation, CancellationToken ct = default)
    {
        db.Conversations.Add(conversation);
        return Task.CompletedTask;
    }
}

public sealed class AnnotationRepository(LoomDbContext db) : IAnnotationRepository
{
    public Task<Loom.Domain.Annotations.Annotation?> GetAsync(Loom.Domain.Annotations.AnnotationId id, CancellationToken ct = default) =>
        db.Annotations.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Loom.Domain.Annotations.Annotation>> GetByArtifactAsync(Guid artifactId, CancellationToken ct = default) =>
        await db.Annotations.Where(a => a.ArtifactId == artifactId).OrderByDescending(a => a.CreatedAt).ToListAsync(ct);

    public Task AddAsync(Loom.Domain.Annotations.Annotation annotation, CancellationToken ct = default)
    {
        db.Annotations.Add(annotation);
        return Task.CompletedTask;
    }
}

public sealed class SubscriptionRepository(LoomDbContext db) : ISubscriptionRepository
{
    public Task<Loom.Domain.Notifications.Subscription?> GetAsync(Loom.Domain.Notifications.SubscriptionId id, CancellationToken ct = default) =>
        db.Subscriptions.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<Loom.Domain.Notifications.Subscription>> FindForEventAsync(NodeId nodeId, Loom.Domain.Notifications.SubscriptionEventType eventType, CancellationToken ct = default) =>
        await db.Subscriptions.Where(s => s.NodeId == nodeId && s.EventType == eventType).ToListAsync(ct);

    public async Task<IReadOnlyList<Loom.Domain.Notifications.Subscription>> ListByUserAsync(Guid userId, CancellationToken ct = default) =>
        await db.Subscriptions.Where(s => s.UserId == userId).OrderByDescending(s => s.UpdatedAt).ToListAsync(ct);

    public Task AddAsync(Loom.Domain.Notifications.Subscription subscription, CancellationToken ct = default)
    {
        db.Subscriptions.Add(subscription);
        return Task.CompletedTask;
    }

    public void Remove(Loom.Domain.Notifications.Subscription subscription) => db.Subscriptions.Remove(subscription);
}
