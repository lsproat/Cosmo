using Cosmo.Application.Abstractions;
using Cosmo.Domain.Conversations;
using MediatR;

namespace Cosmo.Application.Chat.ContinueConversation;

public record ContinueConversationCommand(Guid ConversationId, string Message) : IRequest<ContinueConversationResult>;

public sealed class ContinueConversationHandler(
    IModelProvider modelProvider,
    IConversationRepository conversationRepository,
    IConversationContextBuilder conversationContextBuilder)
    : IRequestHandler<ContinueConversationCommand, ContinueConversationResult>
{
    public async Task<ContinueConversationResult> Handle(
        ContinueConversationCommand request,
        CancellationToken cancellationToken)
    {
        // Todo: Customize exception type for not found. Maybe a NotFoundException.
        var conversation = conversationRepository.Get(request.ConversationId)
            ?? throw new ArgumentException($"Conversation with ID {request.ConversationId} not found.");

        // Todo: Handle failed message send. Maybe undo message add.
        conversation.AddMessage(request.Message, ConversationMessageRole.User);

        var context = conversationContextBuilder.Build(conversation, cancellationToken);

        var response = await modelProvider.SendMessageAsync(context, cancellationToken);

        conversation.AddMessage(response.Content, ConversationMessageRole.Assistant);

        return new ContinueConversationResult(response.Content);
    }
}
