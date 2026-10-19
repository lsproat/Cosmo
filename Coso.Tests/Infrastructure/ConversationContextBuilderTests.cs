using Cosmo.Domain.Conversations;
using Cosmo.Infrastructure.Configuration;
using Cosmo.Infrastructure.Conversations;
using Cosmo.Tests.Support;
using Microsoft.Extensions.Options;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Cosmo.Tests.Infrastructure;

public class ConversationContextBuilderTests
{
    [Fact]
    public void Build_UnorderedHistory_SortsOnlyStoredMessagesAndKeepsPendingInputLast()
    {
        var conversation = new Conversation("First");
        conversation.AddAssistantMessage("Answer");
        conversation.AddUserMessage("Second");
        conversation.AddAssistantMessage("Another answer");
        var messages = Assert.IsType<List<ConversationMessage>>(conversation.Messages);
        messages.Reverse();
        var stored = messages.ToArray();

        var context = CreateBuilder(new FixedTimeProvider()).Build(conversation, "Pending", CancellationToken.None);

        Assert.Equal(ConversationMessageRole.System, context[0].Role);
        Assert.Equal(new[] { "First", "Answer", "Second", "Another answer", "Pending" }, context.Skip(1).Select(message => message.Content));
        Assert.Equal(stored, conversation.Messages);
    }

    [Fact]
    public void Build_PrependsConfiguredPromptAndLocalDateAndTime()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var context = CreateBuilder(new FixedTimeProvider()).Build(new Conversation(), "Hello", CancellationToken.None);
            Assert.Equal(ConversationMessageRole.System, context[0].Role);
            Assert.Equal("Custom instructions\n\nCurrent date: January 1, 2030\nCurrent time: 7:05 PM",
                context[0].Content.Replace("\r\n", "\n"));
        }
        finally { CultureInfo.CurrentCulture = previousCulture; }
    }

    [Fact]
    public void Build_PreservesHistoryAndAppendsPendingMessageWithoutMutatingConversation()
    {
        var conversation = new Conversation("First question");
        conversation.AddAssistantMessage("Answer");
        var stored = conversation.Messages.ToArray();
        var builder = CreateBuilder(new FixedTimeProvider());

        var context = builder.Build(conversation, "Next question", CancellationToken.None);
        var nextContext = builder.Build(conversation, "Next question", CancellationToken.None);

        Assert.Equal(stored, context.Skip(1).SkipLast(1));
        Assert.Equal(stored, conversation.Messages);
        Assert.Equal("Next question", context[^1].Content);
        Assert.Equal(ConversationMessageRole.User, context[^1].Role);
        Assert.DoesNotContain(context[0], conversation.Messages);
        Assert.DoesNotContain(context[^1], conversation.Messages);
        Assert.NotSame(context, nextContext);
    }

    [Fact]
    public void Build_EmptyHistory_ContainsSystemAndPendingUser()
    {
        var conversation = new Conversation();
        var context = CreateBuilder(new FixedTimeProvider()).Build(conversation, "Hello", CancellationToken.None);
        Assert.Equal(new[] { ConversationMessageRole.System, ConversationMessageRole.User }, context.Select(x => x.Role));
        Assert.Empty(conversation.Messages);
    }

    [Fact]
    public void Build_UsesCurrentTimeOnEachCallAndReturnsSnapshotOfHistory()
    {
        var clock = new FixedTimeProvider();
        var builder = CreateBuilder(clock);
        var conversation = new Conversation("First");
        var first = builder.Build(conversation, "Pending", CancellationToken.None);
        clock.UtcNow = clock.UtcNow.AddDays(1);
        conversation.AddAssistantMessage("Second");
        var second = builder.Build(conversation, "Pending", CancellationToken.None);

        Assert.NotEqual(first[0].Content, second[0].Content);
        Assert.Equal(3, first.Length);
        Assert.Equal(4, second.Length);
        Assert.Equal("Pending", first[^1].Content);
    }

    // The deterministic counter includes a fixed system/formatting cost plus
    // content-length costs for other messages; it does not depend on a model.
    private static int Count(IReadOnlyList<ConversationMessage> messages) =>
        10 + messages.Where(message => message.Role != ConversationMessageRole.System).Sum(message => message.Content.Length);

    [Fact]
    public void Build_ExactlyAtInputLimit_PreservesFullHistory()
    {
        var counter = new StubTokenCounter(Count);
        var conversation = new Conversation("12345");
        conversation.AddAssistantMessage("12345");
        var context = CreateBuilder(new FixedTimeProvider(), counter, inputLimit: 25)
            .Build(conversation, "12345", CancellationToken.None);
        Assert.Equal(25, Count(context));
        Assert.Equal(4, context.Length);
        Assert.Equal(conversation.Messages, context.Skip(1).SkipLast(1));
    }

    [Fact]
    public void Build_OneTokenOverLimit_RemovesWholeOldestTurnAndRecounts()
    {
        var counter = new StubTokenCounter(Count);
        var conversation = new Conversation("12345");
        conversation.AddAssistantMessage("12345");
        var stored = conversation.Messages.ToArray();
        var context = CreateBuilder(new FixedTimeProvider(), counter, inputLimit: 24)
            .Build(conversation, "12345", CancellationToken.None);
        Assert.Equal(15, Count(context));
        Assert.Equal(new[] { ConversationMessageRole.System, ConversationMessageRole.User }, context.Select(x => x.Role));
        Assert.Equal(stored, conversation.Messages);
        Assert.Equal("12345", context[^1].Content);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Build_OversizedCurrentMessage_RejectsEvenWhenOldHistoryIsRemovable(bool history)
    {
        var conversation = new Conversation();
        if (history)
        {
            conversation.AddUserMessage("Old");
            conversation.AddAssistantMessage("Answer");
        }
        var stored = conversation.Messages.ToArray();
        var exception = Assert.Throws<ArgumentException>(() =>
            CreateBuilder(new FixedTimeProvider(), new StubTokenCounter(Count), inputLimit: 20)
                .Build(conversation, "12345678901", CancellationToken.None));
        Assert.Contains("latest message and system prompt", exception.Message);
        Assert.Equal(stored, conversation.Messages);
    }

    [Fact]
    public void Build_MinimumContextExactlyAtLimit_RemovesAllHistoryButKeepsCurrentUser()
    {
        var conversation = new Conversation("Old");
        conversation.AddAssistantMessage("Answer");
        var context = CreateBuilder(new FixedTimeProvider(), new StubTokenCounter(Count), inputLimit: 20)
            .Build(conversation, "1234567890", CancellationToken.None);
        Assert.Equal(20, Count(context));
        Assert.Equal(2, context.Length);
        Assert.Equal("1234567890", context[^1].Content);
    }

    [Fact]
    public void Build_MultipleTrims_PreservesMostRecentCompleteTurnAndPendingMessage()
    {
        var conversation = new Conversation("11111");
        conversation.AddAssistantMessage("aaaaa");
        conversation.AddUserMessage("22222");
        conversation.AddAssistantMessage("bbbbb");
        conversation.AddUserMessage("33333");
        conversation.AddAssistantMessage("ccccc");
        var stored = conversation.Messages.ToArray();
        var counter = new StubTokenCounter(Count);

        var context = CreateBuilder(new FixedTimeProvider(), counter, inputLimit: 25)
            .Build(conversation, "44444", CancellationToken.None);

        Assert.Equal(new[] { "33333", "ccccc", "44444" }, context.Skip(1).Select(message => message.Content));
        Assert.Equal(25, Count(context));
        Assert.Equal(stored, conversation.Messages);
        Assert.Equal(new[] { 45, 35, 25 }, counter.Calls.Skip(1).Select(Count));
    }

    [Fact]
    public void Build_ConsecutiveUsers_TrimsUpToNextUserWithoutDiscardingPendingInput()
    {
        var conversation = new Conversation("11111");
        conversation.AddUserMessage("22222");
        conversation.AddAssistantMessage("aaaaa");

        var context = CreateBuilder(new FixedTimeProvider(), new StubTokenCounter(Count), inputLimit: 25)
            .Build(conversation, "33333", CancellationToken.None);

        Assert.Equal(new[] { "22222", "aaaaa", "33333" }, context.Skip(1).Select(message => message.Content));
        Assert.Equal(25, Count(context));
    }

    [Fact]
    public void Build_SystemPromptAloneExceedsBudget_Rejects()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateBuilder(new FixedTimeProvider(), new StubTokenCounter(Count), inputLimit: 9)
                .Build(new Conversation(), "", CancellationToken.None));
    }

    [Fact]
    public void Build_CancelledBeforeStart_DoesNotCountTokens()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var counter = new StubTokenCounter(Count);
        Assert.Throws<OperationCanceledException>(() =>
            CreateBuilder(new FixedTimeProvider(), counter).Build(new Conversation(), "Pending", cancellation.Token));
        Assert.Empty(counter.Calls);
    }

    [Fact]
    public void Build_CancelledDuringCounting_DoesNotReturnContext()
    {
        using var cancellation = new CancellationTokenSource();
        var counter = new StubTokenCounter(messages =>
        {
            cancellation.Cancel();
            return Count(messages);
        });
        Assert.Throws<OperationCanceledException>(() =>
            CreateBuilder(new FixedTimeProvider(), counter).Build(new Conversation(), "Pending", cancellation.Token));
    }

    [Fact]
    public void Build_LogsTokenBudgetAndRemovedTurnsWithoutMessageContents()
    {
        var logger = new RecordingLogger<ConversationContextBuilder>();
        var conversation = new Conversation("11111");
        conversation.AddAssistantMessage("aaaaa");
        conversation.AddUserMessage("22222");
        conversation.AddAssistantMessage("bbbbb");
        conversation.AddUserMessage("33333");
        conversation.AddAssistantMessage("ccccc");

        CreateBuilder(new FixedTimeProvider(), new StubTokenCounter(Count), 25, logger)
            .Build(conversation, "44444", CancellationToken.None);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("test-model", entry.Values["Model"]);
        Assert.Equal(45, entry.Values["OriginalTokens"]);
        Assert.Equal(25, entry.Values["SelectedTokens"]);
        Assert.Equal(2, entry.Values["RemovedTurns"]);
        Assert.Equal(4, entry.Values["RemovedMessages"]);
        Assert.Equal(25, entry.Values["InputTokenLimit"]);
        Assert.Equal(125, entry.Values["ContextWindowTokens"]);
        Assert.Equal(100, entry.Values["ReservedOutputTokens"]);
        Assert.DoesNotContain("44444", string.Join(" ", entry.Values.Values));
    }

    [Fact]
    public void Build_RejectionLogsMinimumBudget()
    {
        var logger = new RecordingLogger<ConversationContextBuilder>();
        Assert.Throws<ArgumentException>(() => CreateBuilder(new FixedTimeProvider(), new StubTokenCounter(Count), 10, logger)
            .Build(new Conversation(), "Pending", CancellationToken.None));
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(17, entry.Values["MinimumTokens"]);
        Assert.Equal(10, entry.Values["InputTokenLimit"]);
    }

    private static ConversationContextBuilder CreateBuilder(TimeProvider clock, StubTokenCounter? counter = null, int inputLimit = 900,
        ILogger<ConversationContextBuilder>? logger = null) => new(
        counter ?? new StubTokenCounter(),
        Options.Create(new CosmoOptions { BaseSystemPrompt = "Custom instructions" }),
        Options.Create(new OllamaOptions
        {
            BaseUrl = "https://ollama.invalid", Model = "test-model",
            ContextWindowTokens = inputLimit + 100, ReservedOutputTokens = 100
        }), clock, logger);
}
