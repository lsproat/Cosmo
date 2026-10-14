using Cosmo.Application.Abstractions;
using Cosmo.Domain.Conversations;
using Cosmo.Infrastructure.Configuration;
using Cosmo.Infrastructure.Conversations;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

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
        _ => Task.FromResult(new ModelResponse("Model reply", 42));

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
        new StubTokenCounter(),
        Options.Create(new CosmoOptions { BaseSystemPrompt = "Test system prompt" }),
        Options.Create(new OllamaOptions
        {
            BaseUrl = "https://ollama.invalid", Model = "test-model",
            ContextWindowTokens = 1000, ReservedOutputTokens = 100
        }), new FixedTimeProvider());

    public Conversation? Conversation { get; private set; }
    public ConversationMessage[]? Context { get; private set; }
    public CancellationToken Token { get; private set; }

    public ConversationMessage[] Build(Conversation conversation, string newUserMessage, CancellationToken cancellationToken)
    {
        Conversation = conversation;
        Token = cancellationToken;
        return Context = _inner.Build(conversation, newUserMessage, cancellationToken);
    }
}

internal sealed class StubTokenCounter(Func<IReadOnlyList<ConversationMessage>, int>? count = null) : ITokenCounter
{
    public List<ConversationMessage[]> Calls { get; } = [];

    public int CountTokens(IReadOnlyList<ConversationMessage> messages)
    {
        Calls.Add(messages.ToArray());
        return count?.Invoke(messages) ?? messages.Count;
    }
}

internal sealed class RecordingRepository : IConversationRepository
{
    private readonly Dictionary<Guid, Conversation> _conversations = [];
    public int AddCalls { get; private set; }
    public int SaveCalls { get; private set; }
    public CancellationToken SaveToken { get; private set; }
    public Func<CancellationToken, Task>? Save { get; set; }

    public Conversation? Get(Guid id) => _conversations.GetValueOrDefault(id);
    public void Seed(Conversation conversation) => _conversations.Add(conversation.Id, conversation);

    public void Add(Conversation conversation)
    {
        AddCalls++;
        _conversations.Add(conversation.Id, conversation);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCalls++;
        SaveToken = cancellationToken;
        return Save?.Invoke(cancellationToken) ?? Task.CompletedTask;
    }
}

internal sealed class RecordingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, IReadOnlyDictionary<string, object?> Values)> Entries { get; } = [];
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var values = ((IEnumerable<KeyValuePair<string, object?>>)(object)state!).ToDictionary(pair => pair.Key, pair => pair.Value);
        Entries.Add((logLevel, values));
    }
}
