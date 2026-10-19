namespace Cosmo.Application.Chat.GetAllConversations;

public sealed record ConversationSummary(
    Guid Id,
    string Title,
    DateTimeOffset CreatedAt);
