using Cosmo.Application.Abstractions;
using Cosmo.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Cosmo.Infrastructure.Users;

public sealed class UserResolver(CosmoDbContext dbContext)
    : IUserResolver
{
    private const string IdentityConstraint =
        "UQ_Users_ExternalIssuer_ExternalSubject";

    public async Task<int> GetOrCreateUserIdAsync(
        ExternalUserIdentity identity,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.Issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.Subject);

        var existingId = await FindUserIdAsync(
            identity,
            cancellationToken);

        if (existingId.HasValue)
            return existingId.Value;

        var user = new User(identity.Issuer, identity.Subject);
        dbContext.Users.Add(user);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return user.Id;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: IdentityConstraint
            })
        {
            // Another request inserted this identity after our lookup.
            // Remove our failed insert from EF's change tracker.
            dbContext.Entry(user).State = EntityState.Detached;

            var concurrentUserId = await FindUserIdAsync(
                identity,
                cancellationToken);

            if (concurrentUserId.HasValue)
                return concurrentUserId.Value;

            throw;
        }
    }

    private Task<int?> FindUserIdAsync(
        ExternalUserIdentity identity,
        CancellationToken cancellationToken)
    {
        return dbContext.Users
            .AsNoTracking()
            .Where(user =>
                user.ExternalIssuer == identity.Issuer &&
                user.ExternalSubject == identity.Subject)
            .Select(user => (int?)user.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }
}
