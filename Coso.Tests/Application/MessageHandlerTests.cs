using Cosmo.Application.Abstractions;
using Cosmo.Application.Chat.ContinueConversation;
using Cosmo.Application.Chat.CreateConversation;
using Cosmo.Application.Chat.SendMessage;
using Cosmo.Domain.Conversations;
using Cosmo.Infrastructure.Conversations;
using Cosmo.Tests.Support;

namespace Cosmo.Tests.Application;

public class MessageHandlerTests
{
    [Fact]
    public async Task SendMessage_SendsOnlyUserMessageAndReturnsModelReply()
    {
        var model = new StubModelProvider();
        using var cancellation = new CancellationTokenSource();
        var handler = new SendMessageHandler(model);

        var result = await handler.Handle(new SendMessageCommand("Hello"), cancellation.Token);

        var message = Assert.Single(Assert.Single(model.Calls));
        Assert.Equal(ConversationMessageRole.User, message.Role);
        Assert.Equal("Hello", message.Content);
        Assert.Equal("Model reply", result.Message);
        Assert.Equal(cancellation.Token, model.Token);
    }

    [Fact]
    public async Task CreateConversation_BuildsContextAndStoresCompletedFirstTurn()
    {
        var repository = new InMemoryConversationRepository();
        var context = new RecordingContextBuilder();
        var model = new StubModelProvider();
        using var cancellation = new CancellationTokenSource();
        var handler = new CreateConversationHandler(model, repository, context);
        model.Respond = _ =>
        {
            Assert.Null(repository.Get(context.Conversation!.Id));
            Assert.Equal("Hello", Assert.Single(context.Conversation.Messages).Content);
            return Task.FromResult(new ModelResponse("Model reply"));
        };

        var result = await handler.Handle(new CreateConversationCommand("Hello"), cancellation.Token);

        var stored = Assert.IsType<Conversation>(repository.Get(result.ConversationId));
        Assert.NotEqual(Guid.Empty, result.ConversationId);
        Assert.Equal("Model reply", result.Message);
        Assert.Equal(new[] { "Hello", "Model reply" }, stored.Messages.Select(x => x.Content));
        Assert.Equal(new[] { ConversationMessageRole.User, ConversationMessageRole.Assistant }, stored.Messages.Select(x => x.Role));
        Assert.Equal(context.Context, Assert.Single(model.Calls));
        Assert.Equal(ConversationMessageRole.System, model.Calls[0][0].Role);
        Assert.Equal(cancellation.Token, context.Token);
        Assert.Equal(cancellation.Token, model.Token);
    }

    [Fact]
    public async Task ContinueConversation_IncludesHistoryAndAppendsTurnsInOrder()
    {
        var repository = new InMemoryConversationRepository();
        var conversation = new Conversation("First question");
        conversation.AddMessage("First answer", ConversationMessageRole.Assistant);
        repository.Add(conversation);
        var context = new RecordingContextBuilder();
        var model = new StubModelProvider();
        var handler = new ContinueConversationHandler(model, repository, context);
        using var cancellation = new CancellationTokenSource();

        var result = await handler.Handle(new ContinueConversationCommand(conversation.Id, "Second question"), cancellation.Token);
        await handler.Handle(new ContinueConversationCommand(conversation.Id, "Third question"), cancellation.Token);

        Assert.Equal("Model reply", result.Message);
        Assert.Equal(new[] { "First question", "First answer", "Second question" }, model.Calls[0].Skip(1).Select(x => x.Content));
        Assert.Equal(new[] { "First question", "First answer", "Second question", "Model reply", "Third question" },
            model.Calls[1].Skip(1).Select(x => x.Content));
        Assert.Equal(new[] { ConversationMessageRole.User, ConversationMessageRole.Assistant,
            ConversationMessageRole.User, ConversationMessageRole.Assistant,
            ConversationMessageRole.User, ConversationMessageRole.Assistant }, conversation.Messages.Select(x => x.Role));
        Assert.Equal("Model reply", conversation.Messages[^1].Content);
        Assert.Equal(context.Context, model.Calls[1]);
        Assert.Equal(cancellation.Token, context.Token);
        Assert.Equal(cancellation.Token, model.Token);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("e0887dd3-a29f-49e9-8667-26ddb796df44")]
    public async Task ContinueConversation_MissingId_ThrowsBeforeBuildingContextOrCallingModel(string id)
    {
        var model = new StubModelProvider();
        var context = new RecordingContextBuilder();
        var handler = new ContinueConversationHandler(model, new InMemoryConversationRepository(), context);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(
            new ContinueConversationCommand(Guid.Parse(id), "Hello"), CancellationToken.None));

        Assert.Contains(id, exception.Message);
        Assert.Null(context.Conversation);
        Assert.Empty(model.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateConversation_ModelFailsOrCancels_DoesNotStoreConversation(bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        var failure = cancel ? (Exception)new OperationCanceledException(cancellation.Token) : new HttpRequestException("Unavailable");
        var model = new StubModelProvider { Respond = _ => Task.FromException<ModelResponse>(failure) };
        var repository = new InMemoryConversationRepository();
        var context = new RecordingContextBuilder();
        var handler = new CreateConversationHandler(model, repository, context);

        var actual = await Record.ExceptionAsync(() => handler.Handle(new CreateConversationCommand("Hello"), cancellation.Token));

        Assert.Same(failure, actual);
        Assert.Null(repository.Get(context.Conversation!.Id));
        Assert.Single(context.Conversation.Messages);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ContinueConversation_ModelFailsOrCancels_LeavesUserMessageInHistory(bool cancel)
    {
        // Characterizes the current behavior: rollback is explicitly a production TODO.
        using var cancellation = new CancellationTokenSource();
        var failure = cancel ? (Exception)new OperationCanceledException(cancellation.Token) : new HttpRequestException("Unavailable");
        var model = new StubModelProvider { Respond = _ => Task.FromException<ModelResponse>(failure) };
        var repository = new InMemoryConversationRepository();
        var conversation = new Conversation("Original");
        repository.Add(conversation);
        var handler = new ContinueConversationHandler(model, repository, new RecordingContextBuilder());

        var actual = await Record.ExceptionAsync(() => handler.Handle(
            new ContinueConversationCommand(conversation.Id, "Failed turn"), cancellation.Token));

        Assert.Same(failure, actual);
        Assert.Equal(new[] { "Original", "Failed turn" }, repository.Get(conversation.Id)!.Messages.Select(x => x.Content));
        Assert.All(conversation.Messages, x => Assert.Equal(ConversationMessageRole.User, x.Role));
    }

    [Fact]
    public async Task SendMessage_PropagatesProviderCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var model = new StubModelProvider
        {
            Respond = token =>
            {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
                throw new InvalidOperationException("Cancellation was not propagated.");
            }
        };

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new SendMessageHandler(model).Handle(new SendMessageCommand("Hello"), cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }
}
