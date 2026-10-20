using Cosmo.Application.Chat.GetAllConversations;
using Cosmo.Application.Chat.GetConversation;
using Cosmo.Domain.Conversations;
using Cosmo.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cosmo.Tests.Application;

public class ConversationQueryTests
{
    [Fact]
    public async Task GetConversation_LoadsMessagesBySequenceEvenWhenStoredInDifferentOrder()
    {
        using var db = CreateContext();
        var conversation = new Conversation("Last");
        conversation.AddAssistantMessage("Middle");
        conversation.AddUserMessage("First");
        db.Conversations.Add(conversation);
        for (var i = 0; i < 3; i++)
            db.Entry(conversation.Messages[i]).Property(message => message.SequenceNumber).CurrentValue = 2 - i;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await new GetConversationHandler(db).Handle(new(conversation.Id), CancellationToken.None);

        Assert.Equal(conversation.Id, result.Conversation.Id);
        Assert.Equal(new[] { "First", "Middle", "Last" }, result.Conversation.Messages.Select(message => message.Content));
        Assert.Equal(new[] { 0, 1, 2 }, result.Conversation.Messages.Select(message => message.SequenceNumber));
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetConversation_EmptyConversation_ReturnsEmptyHistory()
    {
        using var db = CreateContext();
        var conversation = new Conversation();
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await new GetConversationHandler(db).Handle(new(conversation.Id), CancellationToken.None);

        Assert.Empty(result.Conversation.Messages);
        Assert.Equal(string.Empty, result.Conversation.Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Queries_PropagateCancellation(bool list)
    {
        using var db = CreateContext();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            if (list)
                await new GetAllConversationsHandler(db).Handle(new(), cancellation.Token);
            else
                await new GetConversationHandler(db).Handle(new(int.MaxValue), cancellation.Token);
        });
    }

    private static CosmoDbContext CreateContext() => new(
        new DbContextOptionsBuilder<CosmoDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
