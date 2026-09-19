using Cosmo.Application.Abstractions;
using Cosmo.Domain.Conversations;
using MediatR;

namespace Cosmo.Application.Chat.CreateConversation;

public record CreateConversationCommand(string Message) : IRequest<CreateConversationResult>;
public sealed class CreateConversationHandler(
    IModelProvider modelProvider,
    IConversationRepository conversationRepository) 
    : IRequestHandler<CreateConversationCommand, CreateConversationResult>
{
    public async Task<CreateConversationResult> Handle(
        CreateConversationCommand request,
        CancellationToken cancellationToken)
    {
        var response = await modelProvider.SendMessageAsync(
           [new ConversationMessage()
            {
                Role = ConversationMessageRole.User,
                Content = request.Message
            }], cancellationToken);

        var newConversation = new Conversation();
        newConversation.AddMessage(request.Message, ConversationMessageRole.User);
        newConversation.AddMessage(response.Content, ConversationMessageRole.Assistant);
        conversationRepository.Add(newConversation);
        return new CreateConversationResult(newConversation.Id, response.Content);
    }
}