using Cosmo.Application.Abstractions;
using Cosmo.Domain.Conversations;
using Microsoft.EntityFrameworkCore;

namespace Cosmo.Infrastructure.Conversations;

public sealed class EfConversationRepository(CosmoDbContext context) : IConversationRepository
{
    public Conversation? Get(Guid id) => context.Conversations
        .Include(conversation => conversation.Messages
            .OrderBy(message => message.CreatedAt)
            .ThenBy(message => message.Id))
        .SingleOrDefault(conversation => conversation.Id == id);

    public void Add(Conversation conversation) => context.Conversations.Add(conversation);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
