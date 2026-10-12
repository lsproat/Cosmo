using Cosmo.Domain.Conversations;

namespace Cosmo.Application.Abstractions;

public interface ITokenCounter
{
    int CountTokens(IReadOnlyList<ConversationMessage> messages);
}
