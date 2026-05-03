using Loom.Domain.Conversations;
using Loom.Domain.Nodes;
using Loom.Domain.Runs;

namespace Loom.Application.Abstractions;

public interface IConversationRepository
{
    Task<Conversation?> GetAsync(ConversationId id, CancellationToken ct = default);
    Task<Conversation?> GetByRunAsync(RunId runId, CancellationToken ct = default);
    Task<IReadOnlyList<Conversation>> GetByNodeAsync(NodeId nodeId, CancellationToken ct = default);
    Task AddAsync(Conversation conversation, CancellationToken ct = default);
}
