using Cosmo.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cosmo.Application.Chat.GetConversation;

public record GetConversationQuery(int ConversationId) : IRequest<GetConversationResult>, IUserRequest
{
    public int UserId { get; set; }
}

public sealed class GetConversationHandler(ICosmoDbContext cosmoDbContext)
    : IRequestHandler<GetConversationQuery, GetConversationResult>
{
    public async Task<GetConversationResult> Handle(
        GetConversationQuery request,
        CancellationToken cancellationToken)
    {
        var conversation = await cosmoDbContext.Conversations
            .AsNoTracking()
            .Include(convo => convo.Messages.OrderBy(message => message.SequenceNumber))
            .FirstOrDefaultAsync(convo => convo.UserId == request.UserId && convo.Id == request.ConversationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Conversation with ID {request.ConversationId} not found.");

        return new GetConversationResult(conversation);
    }
}
