using Cosmo.Domain.Conversations;
using Cosmo.Infrastructure.Tokenization.Gemma;

namespace Cosmo.Tests.Infrastructure;

public class Gemma4TokenCounterTests(Gemma4TokenCounter counter) : IClassFixture<Gemma4TokenCounter>
{
    [Fact]
    public void FormatConversation_UsesThinkingMarkerAndModelRoleWithoutChangingMessages()
    {
        ConversationMessage[] messages = [
            new(ConversationMessageRole.System, "  Instructions  "),
            new(ConversationMessageRole.User, "  Hello\nこんにちは  "),
            new(ConversationMessageRole.Assistant, " Reply ")];

        var formatted = counter.FormatGemma4Conversation(messages);

        Assert.Equal("<bos><|turn>system\n<|think|>\nInstructions<turn|>\n" +
            "<|turn>user\nHello\nこんにちは<turn|>\n" +
            "<|turn>model\nReply<turn|>\n<|turn>model\n", formatted);
        Assert.Equal("  Hello\nこんにちは  ", messages[1].Content);
    }

    [Fact]
    public void CountTokens_EmptyConversationCountsBosAndGenerationHeader()
    {
        // BOS, turn marker, model role, and newline are each one token.
        Assert.Equal(4, counter.CountTokens([]));
    }

    [Fact]
    public void CountTokens_LeadingAndTrailingWhitespaceMatchesRenderedPrompt()
    {
        ConversationMessage[] trimmed = [new(ConversationMessageRole.System, "Instructions"), new(ConversationMessageRole.User, "café 🚀")];
        ConversationMessage[] padded = [new(ConversationMessageRole.System, " Instructions "), new(ConversationMessageRole.User, "\t café 🚀 \r\n")];
        var count = counter.CountTokens(trimmed);
        Assert.True(count > 4);
        Assert.Equal(count, counter.CountTokens(padded));
        Assert.Equal(count, counter.CountTokens(trimmed));
    }

    [Fact]
    public void CountTokens_UnsupportedRole_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => counter.CountTokens([new((ConversationMessageRole)999, "Invalid")]));
    }
}
