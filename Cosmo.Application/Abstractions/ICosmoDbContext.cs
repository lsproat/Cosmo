using Cosmo.Domain.Conversations;
using Cosmo.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Cosmo.Application.Abstractions;

public interface ICosmoDbContext
{
    DbSet<Conversation> Conversations { get; }
    DbSet<ConversationMessage> ConversationMessages { get; }
    DbSet<User> Users { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
