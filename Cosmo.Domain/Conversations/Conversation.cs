namespace Cosmo.Domain.Conversations;

public class Conversation
{
    private readonly List<ConversationMessage> _messages = [];

    public IReadOnlyList<ConversationMessage> Messages => _messages;

    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public string Title { get; set; } = string.Empty;

    public Conversation()
    { }

    public Conversation(string firstMessage)
    {
        AddUserMessage(firstMessage);

        //Todo: Remove and add logic to create summarized title after first message is sent and response is received.
        Title = firstMessage.Length > 20 ? firstMessage[..20] : firstMessage;
    }

    public void AddUserMessage(string message)
    {
        AddMessage(ConversationMessageRole.User, message);
    }

    public void AddAssistantMessage(string message)
    {
        AddMessage(ConversationMessageRole.Assistant, message);
    }

    private void AddMessage(ConversationMessageRole role, string content)
    {
        var nextSequenceNumber = _messages.Count == 0 ? 0 : checked(_messages[^1].SequenceNumber + 1);
        _messages.Add(new ConversationMessage(role, content, nextSequenceNumber));
    }
}
