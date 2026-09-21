using Cosmo.Application.Abstractions;
using Cosmo.Domain.Conversations;
using MediatR;

namespace Cosmo.Application.Chat.CreateConversation;

public record CreateConversationCommand(string Message) : IRequest<CreateConversationResult>;
public sealed class CreateConversationHandler(
    IModelProvider modelProvider,
    IConversationRepository conversationRepository,
    IConversationContextBuilder conversationContextBuilder)
    : IRequestHandler<CreateConversationCommand, CreateConversationResult>
{
    public async Task<CreateConversationResult> Handle(
        CreateConversationCommand request,
        CancellationToken cancellationToken)
    {

        var newConversation = new Conversation(request.Message);
        var context = conversationContextBuilder.Build(newConversation, cancellationToken);

        var response = await modelProvider.SendMessageAsync(context, cancellationToken);
       
        newConversation.AddMessage(response.Content, ConversationMessageRole.Assistant);
        conversationRepository.Add(newConversation);
        return new CreateConversationResult(newConversation.Id, response.Content);
    }
}