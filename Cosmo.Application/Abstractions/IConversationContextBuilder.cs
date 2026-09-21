using Cosmo.Domain.Conversations;

namespace Cosmo.Application.Abstractions;

public interface IConversationContextBuilder
{
    ConversationMessage[] Build(Conversation conversation,CancellationToken cancellationToken);
}
