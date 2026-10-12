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
        var newConversation = new Conversation();
        var context = conversationContextBuilder.Build(newConversation, request.Message, cancellationToken);

        var response = await modelProvider.SendMessageAsync(context, cancellationToken);

        newConversation.AddUserMessage(request.Message);
        newConversation.AddAssistantMessage(response.Content);
        conversationRepository.Add(newConversation);
        await conversationRepository.SaveChangesAsync(cancellationToken);
        return new CreateConversationResult(newConversation.Id, response.Content);
    }
}
