namespace Cosmo.Domain.Conversations;

public sealed class ConversationMessage(
ConversationMessageRole role,
string content,
// Default to 0 for compatibility. Messages can only be added to a conversation
// from within Conversation class wich will enforce correct ordering.
int sequenceNumber = 0)
{
    public Guid Id { get; } = Guid.NewGuid();

    public int SequenceNumber { get; } = sequenceNumber;

    public ConversationMessageRole Role { get; } = role;

    public string Content { get; } = content;

    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
}
