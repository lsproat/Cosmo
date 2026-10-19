using Cosmo.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cosmo.Application.Chat.ContinueConversation;

public record ContinueConversationCommand(Guid ConversationId, string Message) : IRequest<ContinueConversationResult>;

public sealed class ContinueConversationHandler(
    IModelProvider modelProvider,
    ICosmoDbContext cosmoDbContext,
    IConversationContextBuilder conversationContextBuilder)
    : IRequestHandler<ContinueConversationCommand, ContinueConversationResult>
{
    public async Task<ContinueConversationResult> Handle(
        ContinueConversationCommand request,
        CancellationToken cancellationToken)
    {
        // Todo: Customize exception type for not found. Maybe a NotFoundException.
        var conversation = await cosmoDbContext.Conversations
            .Include(convo => convo.Messages)
            .FirstOrDefaultAsync(convo => convo.Id == request.ConversationId, cancellationToken)
            ?? throw new ArgumentException($"Conversation with ID {request.ConversationId} not found.");

        var context = conversationContextBuilder.Build(conversation, request.Message, cancellationToken);

        var response = await modelProvider.SendMessageAsync(context, cancellationToken);

        conversation.AddUserMessage(request.Message);
        conversation.AddAssistantMessage(response.Content);

        await cosmoDbContext.SaveChangesAsync(cancellationToken);

        return new ContinueConversationResult(response.Content);
    }
}
