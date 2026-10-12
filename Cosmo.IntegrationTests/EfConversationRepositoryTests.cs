using Cosmo.Domain.Conversations;
using Cosmo.Infrastructure;
using Cosmo.Infrastructure.Conversations;
using Microsoft.EntityFrameworkCore;

namespace Cosmo.IntegrationTests;

public class EfConversationRepositoryTests
{
    [Fact]
    public void Add_WithoutSave_DoesNotPersistConversation()
    {
        var options = CreateOptions();
        var conversation = new Conversation("Pending");
        using (var context = new CosmoDbContext(options))
        {
            new EfConversationRepository(context).Add(conversation);
        }

        using var freshContext = new CosmoDbContext(options);
        Assert.Null(new EfConversationRepository(freshContext).Get(conversation.Id));
    }

    [Fact]
    public async Task SaveAndReload_PreservesIdsContentRolesAndAppendedExchange()
    {
        var options = CreateOptions();
        var conversation = new Conversation("Hello");
        conversation.AddAssistantMessage("Hi!");
        var originalIds = conversation.Messages.Select(message => message.Id).ToArray();
        using (var context = new CosmoDbContext(options))
        {
            var repository = new EfConversationRepository(context);
            repository.Add(conversation);
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        using (var context = new CosmoDbContext(options))
        {
            var repository = new EfConversationRepository(context);
            var loaded = Assert.IsType<Conversation>(repository.Get(conversation.Id));
            Assert.NotSame(conversation, loaded);
            Assert.Equal(conversation.CreatedAt, loaded.CreatedAt);
            Assert.Equal(originalIds.Order(), loaded.Messages.Select(message => message.Id).Order());
            foreach (var original in conversation.Messages)
            {
                var restored = Assert.Single(loaded.Messages, message => message.Id == original.Id);
                Assert.Equal(original.Content, restored.Content);
                Assert.Equal(original.Role, restored.Role);
                Assert.Equal(original.CreatedAt, restored.CreatedAt);
            }
            loaded.AddUserMessage("Next question");
            loaded.AddAssistantMessage("Next answer");
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        using var finalContext = new CosmoDbContext(options);
        var result = new EfConversationRepository(finalContext).Get(conversation.Id)!;
        Assert.Equal(4, result.Messages.Count);
        Assert.Contains(result.Messages, message => message.Content == "Next question" && message.Role == ConversationMessageRole.User);
        Assert.Contains(result.Messages, message => message.Content == "Next answer" && message.Role == ConversationMessageRole.Assistant);
    }

    [Fact]
    public async Task LoadedConversation_ChangesDiscardedWithoutSave_DoNotPersist()
    {
        var options = CreateOptions();
        var conversation = new Conversation("Successful question");
        conversation.AddAssistantMessage("Successful answer");
        using (var context = new CosmoDbContext(options))
        {
            var repository = new EfConversationRepository(context);
            repository.Add(conversation);
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        using (var context = new CosmoDbContext(options))
        {
            var loaded = new EfConversationRepository(context).Get(conversation.Id)!;
            loaded.AddUserMessage("Failed request");
        }

        using var freshContext = new CosmoDbContext(options);
        var stored = new EfConversationRepository(freshContext).Get(conversation.Id)!;
        Assert.Equal(2, stored.Messages.Count);
        Assert.DoesNotContain(stored.Messages, message => message.Content == "Failed request");
    }

    private static DbContextOptions<CosmoDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<CosmoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
}
