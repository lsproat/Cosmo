using Cosmo.Domain.Conversations;

namespace Cosmo.Application.Abstractions;

public interface IModelProvider
{
    public Task<ModelResponse> SendMessageAsync(IReadOnlyList<ConversationMessage> messages, CancellationToken cancellationToken);
}
