using Loom.Domain.Conversations;
using Loom.Domain.Nodes;
using Loom.Domain.Runs;

namespace Loom.Application.Conversations;

public interface IConversationService
{
    Task<Conversation> StartForRunAsync(NodeId nodeId, RunId runId, string topic, CancellationToken ct = default);
    Task AppendHumanMessageAsync(ConversationId conversationId, Guid userId, string content, CancellationToken ct = default);
    Task AppendAgentMessageAsync(ConversationId conversationId, RunId runId, string content, CancellationToken ct = default);
    Task<Conversation?> GetByRunAsync(RunId runId, CancellationToken ct = default);
    Task<IReadOnlyList<Conversation>> GetByNodeAsync(NodeId nodeId, CancellationToken ct = default);
}
