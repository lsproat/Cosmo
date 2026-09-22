using Cosmo.Application.Abstractions;
using Cosmo.Domain.Conversations;
using Cosmo.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Cosmo.Infrastructure.Conversations;

public sealed class ConversationContextBuilder(
    IOptions<CosmoOptions> options,
    TimeProvider timeProvider) : IConversationContextBuilder
{
    private readonly CosmoOptions _options = options.Value;

    public ConversationMessage[] Build(Conversation conversation, CancellationToken cancellationToken)
    {
        var currDateTimeOffset = timeProvider.GetLocalNow();

        var systemContext = $"""
            {_options.BaseSystemPrompt}

            Current date: {currDateTimeOffset:MMMM d, yyyy}
            Current time: {currDateTimeOffset:h:mm tt}
            """;
        return [new(
                    ConversationMessageRole.System,
                    systemContext),
                .. conversation.Messages
                ];
    }
}
