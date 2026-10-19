namespace Cosmo.Domain.Conversations;

public class Conversation
{
    private readonly List<ConversationMessage> _messages = [];

    public IReadOnlyList<ConversationMessage> Messages => _messages;

    public int Id { get; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public string Title { get; set; } = string.Empty;

    public int UserId { get; set; }

    public Conversation()
    { }

    public Conversation(string firstMessage)
    {
        AddUserMessage(firstMessage);
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
        var nextSequenceNumber = 0;
        if (_messages.Count == 0)
        {
            //Todo: Remove and add logic to create summarized title after first message is sent and response is received.
            Title = content.Length > 20 ? content[..20] + "..." : content;
        }
        else
        {
            nextSequenceNumber = checked(_messages.Max(message => message.SequenceNumber) + 1);
        }
        _messages.Add(new ConversationMessage(role, content, nextSequenceNumber));
    }
}
