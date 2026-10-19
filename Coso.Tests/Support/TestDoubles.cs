using Microsoft.EntityFrameworkCore;
using Cosmo.Application.Abstractions;
using Cosmo.Domain.Conversations;
using Cosmo.Infrastructure.Configuration;
using Cosmo.Infrastructure;
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

internal sealed class RecordingDbContext : ICosmoDbContext, IDisposable
{
    private readonly CosmoDbContext _context = new(
        new DbContextOptionsBuilder<CosmoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    public DbSet<Conversation> Conversations => _context.Conversations;
    public DbSet<ConversationMessage> ConversationMessages => _context.ConversationMessages;
    public int AddCalls { get; private set; }
    public int SaveCalls { get; private set; }
    public CancellationToken SaveToken { get; private set; }
    public Func<CancellationToken, Task>? Save { get; set; }

    public RecordingDbContext()
    {
        _context.ChangeTracker.Tracked += (_, args) =>
        {
            if (args.Entry.Entity is Conversation && args.Entry.State == EntityState.Added) AddCalls++;
        };
    }

    public Conversation? Get(Guid id) => Conversations.Local.SingleOrDefault(c => c.Id == id);
    public void Seed(Conversation conversation)
    {
        Conversations.Add(conversation);
        _context.SaveChanges();
        AddCalls--;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCalls++;
        SaveToken = cancellationToken;
        if (Save is not null) await Save(cancellationToken);
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public void Dispose() => _context.Dispose();
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
