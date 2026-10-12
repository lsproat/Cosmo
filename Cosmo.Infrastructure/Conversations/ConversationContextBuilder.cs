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

    public ConversationMessage[] Build(Conversation conversation, CancellationToken cancellationToken)
    {
        var currDateTimeOffset = timeProvider.GetLocalNow();
        var systemContext = $"""
            {_options.BaseSystemPrompt}

            Current date: {currDateTimeOffset:MMMM d, yyyy}
            Current time: {currDateTimeOffset:h:mm tt}
            """;
        ConversationMessage[] conversationArray = [new(
                    ConversationMessageRole.System,
                    systemContext),
                .. conversation.Messages
                ];

        var tokenCount = _tokenCounter.CountTokens(conversationArray);

        if (tokenCount > InputTokenLimit)
        {
            // Trim
        }

        return conversationArray;
    }
}
