using Loom.Domain.Common;
using Loom.Domain.Nodes;
using Loom.Domain.Runs;

namespace Loom.Domain.Conversations;

public readonly record struct ConversationId(Guid Value) : IEntityId
{
    public static ConversationId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString("N");
}

public readonly record struct MessageId(Guid Value) : IEntityId
{
    public static MessageId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString("N");
}

/// <summary>
/// Persisted thread of messages between humans + agents associated with a
/// node. The kickoff Run produces a Conversation that captures the
/// transcript, the agent's discovery output, and any PO edits at the gate;
/// follow-on agents read it instead of re-prompting from scratch.
/// </summary>
public sealed class Conversation
{
    private readonly List<Message> _messages = [];

    private Conversation() { } // EF Core

    private Conversation(
        ConversationId id,
        NodeId nodeId,
        RunId? runId,
        string topic,
        DateTimeOffset createdAt)
    {
        Id = id;
        NodeId = nodeId;
        RunId = runId;
        Topic = topic;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public ConversationId Id { get; private set; }
    public NodeId NodeId { get; private set; }

    /// <summary>Run that produced this conversation, if any. Null for ad-hoc human threads.</summary>
    public RunId? RunId { get; private set; }

    public string Topic { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<Message> Messages => _messages.AsReadOnly();

    public static Conversation Create(NodeId nodeId, RunId? runId, string topic, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        return new Conversation(ConversationId.New(), nodeId, runId, topic.Trim(), now);
    }

    public Message AddMessage(MessageAuthor author, string content, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        ArgumentNullException.ThrowIfNull(author);
        var msg = Message.Create(Id, author, content, now);
        _messages.Add(msg);
        UpdatedAt = now;
        return msg;
    }
}

/// <summary>One turn in a Conversation. Author identifies whether it was produced by a human or an agent run.</summary>
public sealed class Message
{
    private Message() { } // EF Core

    private Message(MessageId id, ConversationId conversationId, MessageAuthor author, string content, DateTimeOffset createdAt)
    {
        Id = id;
        ConversationId = conversationId;
        Author = author;
        Content = content;
        CreatedAt = createdAt;
    }

    public MessageId Id { get; private set; }
    public ConversationId ConversationId { get; private set; }
    public MessageAuthor Author { get; private set; } = null!;
    public string Content { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    internal static Message Create(ConversationId conversationId, MessageAuthor author, string content, DateTimeOffset now) =>
        new(MessageId.New(), conversationId, author, content, now);
}

public enum MessageAuthorKind
{
    Human = 1,
    Agent = 2,
    System = 3
}

/// <summary>Identifies who produced a Message: a human user, an agent run, or the system itself.</summary>
public sealed record MessageAuthor(MessageAuthorKind Kind, Guid? UserId, Guid? RunId);
