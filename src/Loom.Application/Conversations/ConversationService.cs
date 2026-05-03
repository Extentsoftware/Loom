using Loom.Application.Abstractions;
using Loom.Domain.Common;
using Loom.Domain.Conversations;
using Loom.Domain.Nodes;
using Loom.Domain.Runs;

namespace Loom.Application.Conversations;

public sealed class ConversationService(
    IConversationRepository conversations,
    IUnitOfWork uow,
    ISystemClock clock) : IConversationService
{
    public async Task<Conversation> StartForRunAsync(NodeId nodeId, RunId runId, string topic, CancellationToken ct = default)
    {
        var convo = Conversation.Create(nodeId, runId, topic, clock.UtcNow);
        await conversations.AddAsync(convo, ct);
        await uow.SaveChangesAsync(ct);
        return convo;
    }

    public async Task AppendHumanMessageAsync(ConversationId conversationId, Guid userId, string content, CancellationToken ct = default)
    {
        var convo = await conversations.GetAsync(conversationId, ct)
            ?? throw new DomainException($"Conversation {conversationId} not found.");
        convo.AddMessage(new MessageAuthor(MessageAuthorKind.Human, userId, null), content, clock.UtcNow);
        await uow.SaveChangesAsync(ct);
    }

    public async Task AppendAgentMessageAsync(ConversationId conversationId, RunId runId, string content, CancellationToken ct = default)
    {
        var convo = await conversations.GetAsync(conversationId, ct)
            ?? throw new DomainException($"Conversation {conversationId} not found.");
        convo.AddMessage(new MessageAuthor(MessageAuthorKind.Agent, null, runId.Value), content, clock.UtcNow);
        await uow.SaveChangesAsync(ct);
    }

    public Task<Conversation?> GetByRunAsync(RunId runId, CancellationToken ct = default) =>
        conversations.GetByRunAsync(runId, ct);

    public Task<IReadOnlyList<Conversation>> GetByNodeAsync(NodeId nodeId, CancellationToken ct = default) =>
        conversations.GetByNodeAsync(nodeId, ct);
}
