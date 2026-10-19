namespace Cosmo.Application.Chat.GetAllConversations;

public sealed record ConversationSummary(
    int Id,
    string Title,
    DateTimeOffset CreatedAt);
