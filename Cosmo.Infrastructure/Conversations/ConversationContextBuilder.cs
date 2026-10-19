using Cosmo.Application.Abstractions;
using Cosmo.Domain.Conversations;
using Cosmo.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cosmo.Infrastructure.Conversations;

// Todo: Pull out Ollama specifc settings. Perhaps abstracted ModelSettings.
public sealed class ConversationContextBuilder(
    ITokenCounter tokenCounter,
    IOptions<CosmoOptions> cosmoOptions,
    IOptions<OllamaOptions> ollamaOptions,
    TimeProvider timeProvider,
    ILogger<ConversationContextBuilder>? logger = null) : IConversationContextBuilder
{
    private readonly ITokenCounter _tokenCounter = tokenCounter;
    private readonly CosmoOptions _options = cosmoOptions.Value;
    private readonly OllamaOptions _ollamaOptions = ollamaOptions.Value;
    private readonly ILogger<ConversationContextBuilder> _logger = logger ?? NullLogger<ConversationContextBuilder>.Instance;
    private int InputTokenLimit => _ollamaOptions.ContextWindowTokens - _ollamaOptions.ReservedOutputTokens;

    public ConversationMessage[] Build(Conversation conversation, string newUserMessage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var currDateTimeOffset = timeProvider.GetLocalNow();
        var systemContext = $"""
            {_options.BaseSystemPrompt}

            Current date: {currDateTimeOffset:MMMM d, yyyy}
            Current time: {currDateTimeOffset:h:mm tt}
            """;
        var pendingUserMessage = new ConversationMessage(ConversationMessageRole.User, newUserMessage);
        ConversationMessage[] conversationArray = [new(
                    ConversationMessageRole.System,
                    systemContext),
                ..conversation.Messages.OrderBy(message => message.SequenceNumber), pendingUserMessage
                ];

        // Check if last user message plus system prompt exceeds the input token limit.
        ConversationMessage[] minimumContextMessages = [conversationArray[0], pendingUserMessage];
        var minimumContextTokenCount = _tokenCounter.CountTokens(minimumContextMessages);
        if (minimumContextTokenCount > InputTokenLimit)
        {
            _logger.LogWarning(
                "Context rejected for {Model}: minimum input {MinimumTokens}, input limit {InputTokenLimit}, context window {ContextWindowTokens}, reserved output {ReservedOutputTokens}",
                _ollamaOptions.Model, minimumContextTokenCount, InputTokenLimit,
                _ollamaOptions.ContextWindowTokens, _ollamaOptions.ReservedOutputTokens);
            throw new ArgumentException(
                "The latest message and system prompt exceed the input token limit.");
        }

        var tokenCount = _tokenCounter.CountTokens(conversationArray);
        var originalTokenCount = tokenCount;
        var removedTurns = 0;
        var removedMessages = 0;

        // Todo: Add advanced trimming/summarizing logic.
        // Trim from the oldest user turn up to, but excluding, the next user turn.
        while (tokenCount > InputTokenLimit)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var oldestUserMessageIndexToInclude = Array.FindIndex(conversationArray, 2, message => message.Role == ConversationMessageRole.User);
            if (oldestUserMessageIndexToInclude == -1)
            {
                throw new InvalidOperationException(
                    "No user message found in conversation context.");
            }
            removedTurns += conversationArray[1..oldestUserMessageIndexToInclude]
                .Count(message => message.Role == ConversationMessageRole.User);
            removedMessages += oldestUserMessageIndexToInclude - 1;
            conversationArray = [conversationArray[0], .. conversationArray[oldestUserMessageIndexToInclude..]];
            // Todo: Ensure advanced logic doesn't count tokens within a loop. This gets expensive as the conversation grows.
            tokenCount = _tokenCounter.CountTokens(conversationArray);
        }

        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation(
            "Context selected for {Model}: tokens before {OriginalTokens}, tokens after {SelectedTokens}, removed turns {RemovedTurns}, removed messages {RemovedMessages}, input limit {InputTokenLimit}, context window {ContextWindowTokens}, reserved output {ReservedOutputTokens}",
            _ollamaOptions.Model, originalTokenCount, tokenCount, removedTurns, removedMessages,
            InputTokenLimit, _ollamaOptions.ContextWindowTokens, _ollamaOptions.ReservedOutputTokens);
        return conversationArray;
    }
}
