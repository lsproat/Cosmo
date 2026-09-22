using System.Globalization;
using Cosmo.Domain.Conversations;
using Cosmo.Infrastructure.Configuration;
using Cosmo.Infrastructure.Conversations;
using Cosmo.Tests.Support;
using Microsoft.Extensions.Options;

namespace Cosmo.Tests.Infrastructure;

public class ConversationContextBuilderTests
{
    [Fact]
    public void Build_PrependsConfiguredPromptAndLocalDateAndTime()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var builder = CreateBuilder(new FixedTimeProvider());

            var context = builder.Build(new Conversation("Hello"), CancellationToken.None);

            Assert.Equal(ConversationMessageRole.System, context[0].Role);
            Assert.Equal("Custom instructions\n\nCurrent date: January 1, 2030\nCurrent time: 7:05 PM",
                context[0].Content.Replace("\r\n", "\n"));
        }
        finally { CultureInfo.CurrentCulture = previousCulture; }
    }

    [Fact]
    public void Build_PreservesHistoryOrderRolesAndContentsWithoutModifyingStoredMessages()
    {
        var conversation = new Conversation("First question");
        conversation.AddMessage("Answer", ConversationMessageRole.Assistant);
        conversation.AddMessage("Stored instruction", ConversationMessageRole.System);
        conversation.AddMessage("Next question", ConversationMessageRole.User);
        var stored = conversation.Messages.ToArray();
        var builder = CreateBuilder(new FixedTimeProvider());

        var context = builder.Build(conversation, CancellationToken.None);
        var nextContext = builder.Build(conversation, CancellationToken.None);

        Assert.Equal(stored, context.Skip(1));
        Assert.Equal(stored, nextContext.Skip(1));
        Assert.Equal(stored, conversation.Messages);
        Assert.DoesNotContain(context[0], conversation.Messages);
        Assert.NotSame(context, nextContext);
    }

    [Fact]
    public void Build_EmptyHistory_ContainsOnlySystemContext()
    {
        // Construction requires a first message; the public collection can subsequently be emptied.
        var conversation = new Conversation("Initial message");
        conversation.Messages.Clear();

        var context = CreateBuilder(new FixedTimeProvider()).Build(conversation, CancellationToken.None);

        Assert.Equal(ConversationMessageRole.System, Assert.Single(context).Role);
        Assert.Empty(conversation.Messages);
    }

    [Fact]
    public void Build_UsesCurrentTimeOnEachCallAndReturnsSnapshotOfHistory()
    {
        var clock = new FixedTimeProvider();
        var builder = CreateBuilder(clock);
        var conversation = new Conversation("First");
        var first = builder.Build(conversation, CancellationToken.None);

        clock.UtcNow = clock.UtcNow.AddDays(1);
        conversation.AddMessage("Second", ConversationMessageRole.Assistant);
        var second = builder.Build(conversation, CancellationToken.None);

        Assert.NotEqual(first[0].Content, second[0].Content);
        Assert.Equal(2, first.Length);
        Assert.Equal(3, second.Length);
    }

    private static ConversationContextBuilder CreateBuilder(TimeProvider clock) => new(
        Options.Create(new CosmoOptions { BaseSystemPrompt = "Custom instructions" }), clock);
}
