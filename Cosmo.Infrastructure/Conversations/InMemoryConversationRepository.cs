using Cosmo.Application.Abstractions;
using Cosmo.Domain.Conversations;
using System.Collections.Concurrent;

namespace Cosmo.Infrastructure.Conversations;

public class InMemoryConversationRepository : IConversationRepository
{
    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    private readonly ConcurrentDictionary<Guid, Conversation> _conversations = new();

    public Conversation? Get(Guid id)
    {
        _conversations.TryGetValue(id, out var conversation);
        return conversation;
    }

    public void Add(Conversation conversation)
    {
        if (!_conversations.TryAdd(conversation.Id, conversation))
        {
            throw new InvalidOperationException(
                $"Conversation {conversation.Id} already exists.");
        }
    }
}
