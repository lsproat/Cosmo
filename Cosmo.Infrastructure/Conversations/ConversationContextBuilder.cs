using Cosmo.Application.Abstractions;
using Cosmo.Domain.Conversations;
using Cosmo.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Cosmo.Infrastructure.Conversations;

public sealed class ConversationContextBuilder(IOptions<CosmoOptions> options) : IConversationContextBuilder
{
    private readonly CosmoOptions _options = options.Value;
    public ConversationMessage[] Build(Conversation conversation, CancellationToken cancellationToken)
    {
        return
                [new(
                ConversationMessageRole.System,
                _options.BaseSystemPrompt),
            .. conversation.Messages
                ];
    }
}
