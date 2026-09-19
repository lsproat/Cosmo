using Cosmo.Domain.Conversations;

namespace Cosmo.Application.Abstractions;

public interface IConversationRepository
{
    public Conversation? Get(Guid id);

    public void Add(Conversation conversation);
}
