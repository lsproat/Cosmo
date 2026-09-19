using Cosmo.Application.Abstractions;
using Cosmo.Domain.Conversations;
using MediatR;

namespace Cosmo.Application.Chat.ContinueConversation;

public record ContinueConversationCommand(Guid ConversationId, string Message) : IRequest<ContinueConversationResult>;
public sealed class ContinueConversationHandler(
    IModelProvider modelProvider,
    IConversationRepository conversationRepository) 
    : IRequestHandler<ContinueConversationCommand, ContinueConversationResult>
{
    public async Task<ContinueConversationResult> Handle(
        ContinueConversationCommand request,
        CancellationToken cancellationToken)
    {
        var conversation = conversationRepository.Get(request.ConversationId) 
            ?? throw new ArgumentException($"Conversation with ID {request.ConversationId} not found.");

        conversation.AddMessage(request.Message, ConversationMessageRole.User);

        var response = await modelProvider.SendMessageAsync(conversation.Messages, cancellationToken);

        conversation.AddMessage(response.Content, ConversationMessageRole.Assistant);

        return new ContinueConversationResult(response.Content);
    }
}