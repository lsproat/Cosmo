using Cosmo.Domain.Conversations;

namespace Cosmo.Tests.Domain;

public class ConversationTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("Short title", "Short title")]
    [InlineData("12345678901234567890", "12345678901234567890")]
    [InlineData("123456789012345678901", "12345678901234567890...")]
    public void FirstMessage_SetsTitleWhichLaterMessagesPreserve(string input, string expected)
    {
        var conversation = new Conversation(input);
        conversation.AddAssistantMessage("Reply");
        conversation.AddUserMessage("Follow up");

        Assert.Equal(expected, conversation.Title);
    }

    [Fact]
    public void Constructor_EmptyConversation_AwaitsDatabaseIdentityAndHasNoMessages()
    {
        var conversation = new Conversation();
        Assert.Equal(0, conversation.Id);
        Assert.Empty(conversation.Messages);
        Assert.Equal(TimeSpan.Zero, conversation.CreatedAt.Offset);
    }

    [Fact]
    public void Constructor_CreatesIndependentConversationsWithFirstUserMessage()
    {
        var first = new Conversation("Hello");
        var second = new Conversation("Another chat");

        Assert.Equal(0, first.Id);
        Assert.Equal(0, second.Id);
        var message = Assert.Single(first.Messages);
        Assert.Equal(ConversationMessageRole.User, message.Role);
        Assert.Equal("Hello", message.Content);
        Assert.NotSame(first.Messages, second.Messages);
    }

    [Fact]
    public void AddMessages_AppendsRolesContentsAndSequenceBeforeIdsAreGenerated()
    {
        var conversation = new Conversation("Question");
        var createdAt = conversation.CreatedAt;
        var firstMessageCreatedAt = conversation.Messages[0].CreatedAt;

        conversation.AddAssistantMessage("Answer");
        conversation.AddUserMessage("Follow up");

        Assert.Equal(new[] { "Question", "Answer", "Follow up" }, conversation.Messages.Select(x => x.Content));
        Assert.Equal(new[] { ConversationMessageRole.User, ConversationMessageRole.Assistant,
            ConversationMessageRole.User }, conversation.Messages.Select(x => x.Role));
        Assert.All(conversation.Messages, message => Assert.Equal(0, message.Id));
        Assert.Equal(new[] { 0, 1, 2 }, conversation.Messages.Select(x => x.SequenceNumber));
        Assert.Equal(createdAt, conversation.CreatedAt);
        Assert.Equal(firstMessageCreatedAt, conversation.Messages[0].CreatedAt);
    }

    [Fact]
    public void AddMessage_ContinuesAfterHighestExistingSequenceNumber()
    {
        var conversation = new Conversation("Existing message");
        var messages = Assert.IsType<List<ConversationMessage>>(conversation.Messages);
        messages[0] = new ConversationMessage(ConversationMessageRole.User, "Existing message", 10);
        conversation.AddAssistantMessage("Next message");
        Assert.Equal(11, conversation.Messages[^1].SequenceNumber);
    }
}
