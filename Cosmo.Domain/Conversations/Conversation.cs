namespace Cosmo.Domain.Conversations;

public class Conversation
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public List<ConversationMessage> Messages { get; } = [];

    public Conversation(string firstMessage)
    {
        AddMessage(firstMessage, ConversationMessageRole.User);
    }

    public void AddMessage(string message, ConversationMessageRole role)
    {
        Messages.Add(new ConversationMessage(role, message));
    }
}
