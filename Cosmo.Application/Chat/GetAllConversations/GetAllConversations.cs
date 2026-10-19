using Cosmo.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cosmo.Application.Chat.GetAllConversations;

public record GetAllConversationsQuery : IRequest<GetAllConversationsResult>;

public sealed class GetAllConversationsHandler(ICosmoDbContext cosmoDbContext)
    : IRequestHandler<GetAllConversationsQuery, GetAllConversationsResult>
{
    public async Task<GetAllConversationsResult> Handle(
        GetAllConversationsQuery request,
        CancellationToken cancellationToken)
    {
        var conversationSummaries = await cosmoDbContext.Conversations.Select(conversation => new ConversationSummary(
                conversation.Id,
                conversation.Title,
                conversation.CreatedAt)).ToArrayAsync(cancellationToken);

        return new GetAllConversationsResult(conversationSummaries);
    }
}
