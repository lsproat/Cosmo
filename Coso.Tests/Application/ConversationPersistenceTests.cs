using Cosmo.Application.Abstractions;
using Cosmo.Application.Chat.ContinueConversation;
using Cosmo.Application.Chat.CreateConversation;
using Cosmo.Domain.Conversations;
using Cosmo.Tests.Support;

namespace Cosmo.Tests.Application;

public class ConversationPersistenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuccessfulRequest_SavesCompletedExchangeAfterGeneration(bool continuing)
    {
        using var cancellation = new CancellationTokenSource();
        using var repository = new RecordingDbContext();
        var conversation = new Conversation("Original");
        conversation.AddAssistantMessage("Original answer");
        repository.Seed(conversation);
        var original = conversation.Messages.ToArray();
        var context = new RecordingContextBuilder();
        var generated = false;
        var saved = false;
        var model = new StubModelProvider
        {
            Respond = _ =>
            {
                Assert.Equal(0, repository.SaveCalls);
                Assert.Equal(original, conversation.Messages);
                if (!continuing) Assert.Empty(context.Conversation!.Messages);
                generated = true;
                return Task.FromResult(new ModelResponse("Answer", 42));
            }
        };
        repository.Save = token =>
        {
            Assert.True(generated);
            Assert.Equal(cancellation.Token, token);
            var stored = repository.Get(context.Conversation!.Id)!;
            Assert.Equal(new[] { "Pending", "Answer" }, stored.Messages.TakeLast(2).Select(message => message.Content));
            Assert.DoesNotContain(stored.Messages, message => message.Role == ConversationMessageRole.System);
            saved = true;
            return Task.CompletedTask;
        };

        await Invoke(continuing, repository, model, context, conversation.Id, cancellation.Token);

        Assert.True(saved);
        Assert.Equal(1, repository.SaveCalls);
        Assert.Equal(continuing ? 0 : 1, repository.AddCalls);
        Assert.Equal(cancellation.Token, repository.SaveToken);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ProviderFailureOrCancellation_DoesNotAppendOrSave(bool continuing, bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        var failure = cancel ? (Exception)new OperationCanceledException(cancellation.Token) : new HttpRequestException("Unavailable");
        using var repository = new RecordingDbContext();
        var conversation = new Conversation("Original");
        repository.Seed(conversation);
        var context = new RecordingContextBuilder();
        var model = new StubModelProvider { Respond = _ => Task.FromException<ModelResponse>(failure) };

        var actual = await Record.ExceptionAsync(() =>
            Invoke(continuing, repository, model, context, conversation.Id, cancellation.Token));

        Assert.Same(failure, actual);
        Assert.Equal(0, repository.SaveCalls);
        Assert.Equal(0, repository.AddCalls);
        Assert.Equal("Original", Assert.Single(conversation.Messages).Content);
        if (!continuing) Assert.Empty(context.Conversation!.Messages);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ContextRejection_DoesNotCallModelOrSave(bool continuing)
    {
        using var repository = new RecordingDbContext();
        var conversation = new Conversation("Original");
        repository.Seed(conversation);
        var model = new StubModelProvider();

        await Assert.ThrowsAsync<ArgumentException>(() => Invoke(continuing, repository, model,
            new RejectingContextBuilder(), conversation.Id, CancellationToken.None));

        Assert.Empty(model.Calls);
        Assert.Equal(0, repository.AddCalls);
        Assert.Equal(0, repository.SaveCalls);
        Assert.Equal("Original", Assert.Single(conversation.Messages).Content);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveFailure_PropagatesRatherThanReturningSuccess(bool continuing)
    {
        var failure = new InvalidOperationException("Save failed");
        using var repository = new RecordingDbContext { Save = _ => Task.FromException(failure) };
        var conversation = new Conversation("Original");
        repository.Seed(conversation);
        var model = new StubModelProvider();

        var actual = await Record.ExceptionAsync(() => Invoke(continuing, repository, model,
            new RecordingContextBuilder(), conversation.Id, CancellationToken.None));

        Assert.Same(failure, actual);
        Assert.Single(model.Calls);
        Assert.Equal(1, repository.SaveCalls);
    }

    [Fact]
    public async Task FailedContinuationThenRetry_DoesNotDuplicatePendingUserMessage()
    {
        using var repository = new RecordingDbContext();
        var conversation = new Conversation("Original");
        conversation.AddAssistantMessage("Original answer");
        repository.Seed(conversation);
        var model = new StubModelProvider { Respond = _ => throw new HttpRequestException("Unavailable") };
        var context = new RecordingContextBuilder();
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Invoke(true, repository, model, context, conversation.Id, CancellationToken.None));

        model.Respond = _ => Task.FromResult(new ModelResponse("Answer", 42));
        await Invoke(true, repository, model, context, conversation.Id, CancellationToken.None);

        Assert.Equal(new[] { "Original", "Original answer", "Pending", "Answer" }, conversation.Messages.Select(message => message.Content));
        Assert.Equal(1, repository.SaveCalls);
    }

    private static async Task Invoke(bool continuing, RecordingDbContext repository, IModelProvider model,
        IConversationContextBuilder context, int id, CancellationToken token)
    {
        if (continuing)
            await new ContinueConversationHandler(model, repository, context).Handle(new(id, "Pending"), token);
        else
            await new CreateConversationHandler(model, repository, context).Handle(new("Pending"), token);
    }

    private sealed class RejectingContextBuilder : IConversationContextBuilder
    {
        public ConversationMessage[] Build(Conversation conversation, string newUserMessage, CancellationToken cancellationToken) =>
            throw new ArgumentException("The latest message and system prompt exceed the input token limit.");
    }
}
