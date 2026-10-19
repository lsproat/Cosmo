using Cosmo.Domain.Conversations;
using Microsoft.EntityFrameworkCore;

namespace Cosmo.Application.Abstractions;

public interface ICosmoDbContext
{
    DbSet<Conversation> Conversations { get; }
    DbSet<ConversationMessage> ConversationMessages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
