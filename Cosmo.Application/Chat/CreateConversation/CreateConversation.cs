using Cosmo.Application.Abstractions;
using Cosmo.Domain.Conversations;
using MediatR;

namespace Cosmo.Application.Chat.CreateConversation;

public record CreateConversationCommand(string Message) : IRequest<CreateConversationResult>, IUserRequest
{
    public int UserId { get; set; }
}

public sealed class CreateConversationHandler(
    IModelProvider modelProvider,
    ICosmoDbContext cosmoDbContext,
    IConversationContextBuilder conversationContextBuilder)
    : IRequestHandler<CreateConversationCommand, CreateConversationResult>
{
    public async Task<CreateConversationResult> Handle(
        CreateConversationCommand request,
        CancellationToken cancellationToken)
    {
        var newConversation = new Conversation() { UserId = request.UserId };
        var context = conversationContextBuilder.Build(newConversation, request.Message, cancellationToken);

        var response = await modelProvider.SendMessageAsync(context, cancellationToken);

        newConversation.AddUserMessage(request.Message);
        newConversation.AddAssistantMessage(response.Content);
        cosmoDbContext.Conversations.Add(newConversation);
        await cosmoDbContext.SaveChangesAsync(cancellationToken);
        return new CreateConversationResult(newConversation.Id, response.Content);
    }
}
