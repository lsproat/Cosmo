using Cosmo.Application.Abstractions;
using Cosmo.Domain.Conversations;
using Cosmo.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Cosmo.Infrastructure.Conversations;

// Todo: Pull out Ollama specifc settings. Perhaps abstracted ModelSettings.
public sealed class ConversationContextBuilder(
    ITokenCounter tokenCounter,
    IOptions<CosmoOptions> cosmoOptions,
    IOptions<OllamaOptions> ollamaOptions,
    TimeProvider timeProvider) : IConversationContextBuilder
{
    private readonly ITokenCounter _tokenCounter = tokenCounter;
    private readonly CosmoOptions _options = cosmoOptions.Value;
    private readonly OllamaOptions _ollamaOptions = ollamaOptions.Value;
    private int InputTokenLimit => _ollamaOptions.ContextWindowTokens - _ollamaOptions.ReservedOutputTokens;

    public ConversationMessage[] Build(Conversation conversation, string newUserMessage, CancellationToken cancellationToken)
    {
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
                ..conversation.Messages, pendingUserMessage
                ];

        // Check if last user message plus system prompt exceeds the input token limit.
        ConversationMessage[] minimumContextMessages = [conversationArray[0], pendingUserMessage];
        var minimumContextTokenCount = _tokenCounter.CountTokens(minimumContextMessages);
        if (minimumContextTokenCount > InputTokenLimit)
        {
            throw new ArgumentException(
                "The latest message and system prompt exceed the input token limit.");
        }

        var tokenCount = _tokenCounter.CountTokens(conversationArray);

        // Todo: Add advanced trimming/summarizing logic.
        // Trim oldest messages until we are within the limit. Trim from oldest user message up to and exluding the next user message.
        while (tokenCount > InputTokenLimit)
        {
            // Todo: Check edge cases where there could be multiple user messages in a row.
            var oldestUserMessageIndexToInclude = Array.FindIndex(conversationArray, 2, message => message.Role == ConversationMessageRole.User);
            if (oldestUserMessageIndexToInclude == -1)
            {
                throw new InvalidOperationException(
                    "No user message found in conversation context.");
            }
            conversationArray = [conversationArray[0], .. conversationArray[oldestUserMessageIndexToInclude..]];
            tokenCount = _tokenCounter.CountTokens(conversationArray);
        }

        return conversationArray;
    }
}
