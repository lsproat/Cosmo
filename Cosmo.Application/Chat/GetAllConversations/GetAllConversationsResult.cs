namespace Cosmo.Application.Chat.GetAllConversations;

public sealed class GetAllConversationsResult
{
    public IReadOnlyList<ConversationSummary> Conversations { get; }

    public GetAllConversationsResult(IReadOnlyList<ConversationSummary> conversations)
    {
        Conversations = conversations;
    }
}
