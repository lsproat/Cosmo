namespace Cosmo.Domain.Conversations;

public class ConversationMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public ConversationMessageRole Role { get; init; }

    public required string Content { get; init; }

    public DateTimeOffset CreatedAt { get; init; } =
        DateTimeOffset.UtcNow;
}