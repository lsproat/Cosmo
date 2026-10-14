using Cosmo.Domain.Conversations;

namespace Cosmo.Tests.Domain;

public class ConversationTests
{
    [Fact]
    public void Constructor_EmptyConversation_HasIdentityButNoStoredSystemOrPendingMessage()
    {
        var conversation = new Conversation();
        Assert.NotEqual(Guid.Empty, conversation.Id);
        Assert.Empty(conversation.Messages);
        Assert.Equal(TimeSpan.Zero, conversation.CreatedAt.Offset);
    }

    [Fact]
    public void Constructor_CreatesIndependentConversationsWithFirstUserMessage()
    {
        var first = new Conversation("Hello");
        var second = new Conversation("Another chat");

        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(first.Id, second.Id);
        var message = Assert.Single(first.Messages);
        Assert.Equal(ConversationMessageRole.User, message.Role);
        Assert.Equal("Hello", message.Content);
        Assert.NotSame(first.Messages, second.Messages);
    }

    [Fact]
    public void AddMessages_AppendsRolesAndContentsInInsertionOrderWithDistinctIds()
    {
        var conversation = new Conversation("Question");
        var createdAt = conversation.CreatedAt;
        var firstMessageCreatedAt = conversation.Messages[0].CreatedAt;

        conversation.AddAssistantMessage("Answer");
        conversation.AddUserMessage("Follow up");

        Assert.Equal(new[] { "Question", "Answer", "Follow up" }, conversation.Messages.Select(x => x.Content));
        Assert.Equal(new[] { ConversationMessageRole.User, ConversationMessageRole.Assistant,
            ConversationMessageRole.User }, conversation.Messages.Select(x => x.Role));
        Assert.All(conversation.Messages, message => Assert.NotEqual(Guid.Empty, message.Id));
        Assert.Equal(3, conversation.Messages.Select(x => x.Id).Distinct().Count());
        Assert.Equal(createdAt, conversation.CreatedAt);
        Assert.Equal(firstMessageCreatedAt, conversation.Messages[0].CreatedAt);
    }
}
