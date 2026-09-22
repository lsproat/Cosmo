using Cosmo.Application.Abstractions;
using Cosmo.Domain.Conversations;
using Cosmo.Infrastructure.Configuration;
using Cosmo.Infrastructure.Conversations;
using Microsoft.Extensions.Options;

namespace Cosmo.Tests.Support;

internal sealed class FixedTimeProvider : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = new(2030, 1, 2, 1, 5, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => UtcNow;
    public override TimeZoneInfo LocalTimeZone { get; } = TimeZoneInfo.CreateCustomTimeZone(
        "Test offset", TimeSpan.FromHours(-6), "Test offset", "Test offset");
}

internal sealed class StubModelProvider : IModelProvider
{
    public List<ConversationMessage[]> Calls { get; } = [];
    public CancellationToken Token { get; private set; }
    public Func<CancellationToken, Task<ModelResponse>> Respond { get; set; } =
        _ => Task.FromResult(new ModelResponse("Model reply"));

    public Task<ModelResponse> SendMessageAsync(IReadOnlyList<ConversationMessage> messages, CancellationToken cancellationToken)
    {
        Calls.Add(messages.ToArray());
        Token = cancellationToken;
        return Respond(cancellationToken);
    }
}

internal sealed class RecordingContextBuilder : IConversationContextBuilder
{
    private readonly ConversationContextBuilder _inner = new(
        Options.Create(new CosmoOptions { BaseSystemPrompt = "Test system prompt" }), new FixedTimeProvider());
    public Conversation? Conversation { get; private set; }
    public ConversationMessage[]? Context { get; private set; }
    public CancellationToken Token { get; private set; }

    public ConversationMessage[] Build(Conversation conversation, CancellationToken cancellationToken)
    {
        Conversation = conversation;
        Token = cancellationToken;
        return Context = _inner.Build(conversation, cancellationToken);
    }
}
