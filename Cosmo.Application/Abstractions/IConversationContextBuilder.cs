using Cosmo.Domain.Conversations;

namespace Cosmo.Application.Abstractions;

public interface IConversationContextBuilder
{
    ConversationMessage[] Build(Conversation conversation, string newUserMessage, CancellationToken cancellationToken);
}
