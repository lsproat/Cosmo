namespace Cosmo.Domain.Conversations;

public class Conversation
{
    private readonly List<ConversationMessage> _messages = [];

    public IReadOnlyList<ConversationMessage> Messages => _messages;

    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public Conversation()
    { }

    public Conversation(string firstMessage)
    {
        AddUserMessage(firstMessage);
    }

    public void AddUserMessage(string message)
    {
        _messages.Add(new ConversationMessage(ConversationMessageRole.User, message));
    }

    public void AddAssistantMessage(string message)
    {
        _messages.Add(new ConversationMessage(ConversationMessageRole.Assistant, message));
    }
}
