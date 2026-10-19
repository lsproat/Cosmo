using Cosmo.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cosmo.Application.Chat.GetConversation;

public record GetConversationQuery(Guid ConversationId) : IRequest<GetConversationResult>;

public sealed class GetConversationHandler(ICosmoDbContext cosmoDbContext)
    : IRequestHandler<GetConversationQuery, GetConversationResult>
{
    public async Task<GetConversationResult> Handle(
        GetConversationQuery request,
        CancellationToken cancellationToken)
    {
        var conversation = await cosmoDbContext.Conversations
            .Include(convo => convo.Messages)
            .FirstOrDefaultAsync(convo => convo.Id == request.ConversationId, cancellationToken)
            ?? throw new ArgumentException($"Conversation with ID {request.ConversationId} not found.");

        return new GetConversationResult(conversation);
    }
}
