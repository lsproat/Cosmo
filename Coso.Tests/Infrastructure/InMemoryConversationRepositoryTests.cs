using Cosmo.Domain.Conversations;
using Cosmo.Infrastructure.Conversations;

namespace Cosmo.Tests.Infrastructure;

public class InMemoryConversationRepositoryTests
{
    [Fact]
    public void Get_UnknownId_ReturnsNull()
    {
        Assert.Null(new InMemoryConversationRepository().Get(Guid.NewGuid()));
    }

    [Fact]
    public void Add_StoresIndependentConversationsAndRetainsUpdates()
    {
        var repository = new InMemoryConversationRepository();
        var first = new Conversation("First");
        var second = new Conversation("Second");
        repository.Add(first);
        repository.Add(second);

        repository.Get(first.Id)!.AddMessage("Reply", ConversationMessageRole.Assistant);

        Assert.Equal(new[] { "First", "Reply" }, repository.Get(first.Id)!.Messages.Select(x => x.Content));
        Assert.Equal("Second", Assert.Single(repository.Get(second.Id)!.Messages).Content);
    }

    [Fact]
    public void Add_DuplicateId_ThrowsWithoutReplacingStoredConversation()
    {
        var repository = new InMemoryConversationRepository();
        var original = new Conversation("Original");
        repository.Add(original);

        Assert.Throws<InvalidOperationException>(() => repository.Add(new Conversation("Replacement") { Id = original.Id }));

        Assert.Equal("Original", Assert.Single(repository.Get(original.Id)!.Messages).Content);
    }
}
