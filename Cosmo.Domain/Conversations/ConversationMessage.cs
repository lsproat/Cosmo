namespace Cosmo.Domain.Conversations;

public sealed class ConversationMessage(
ConversationMessageRole role,
string content)
{
    public Guid Id { get; } = Guid.NewGuid();

    public ConversationMessageRole Role { get; } = role;

    public string Content { get; } = content;

    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
}