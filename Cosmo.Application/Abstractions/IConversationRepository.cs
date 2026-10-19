using Cosmo.Domain.Conversations;

namespace Cosmo.Application.Abstractions;

public interface IConversationRepository
{
    public Conversation? Get(int id);

    public void Add(Conversation conversation);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
