using Cosmo.Domain.Conversations;

namespace Cosmo.Tests.Domain;

public class ConversationTests
{
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
    public void AddMessage_AppendsRolesAndContentsInInsertionOrderWithDistinctIds()
    {
        var conversation = new Conversation("Question");
        var createdAt = conversation.CreatedAt;
        var firstMessageCreatedAt = conversation.Messages[0].CreatedAt;

        conversation.AddMessage("Answer", ConversationMessageRole.Assistant);
        conversation.AddMessage("Instruction", ConversationMessageRole.System);
        conversation.AddMessage("Follow up", ConversationMessageRole.User);

        Assert.Equal(new[] { "Question", "Answer", "Instruction", "Follow up" }, conversation.Messages.Select(x => x.Content));
        Assert.Equal(new[] { ConversationMessageRole.User, ConversationMessageRole.Assistant,
            ConversationMessageRole.System, ConversationMessageRole.User }, conversation.Messages.Select(x => x.Role));
        Assert.All(conversation.Messages, message => Assert.NotEqual(Guid.Empty, message.Id));
        Assert.Equal(4, conversation.Messages.Select(x => x.Id).Distinct().Count());
        Assert.Equal(createdAt, conversation.CreatedAt);
        Assert.Equal(firstMessageCreatedAt, conversation.Messages[0].CreatedAt);
    }
}
