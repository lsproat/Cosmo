namespace Cosmo.Application.Abstractions;

public interface IUserResolver
{
    Task<int> GetOrCreateUserIdAsync(
        ExternalUserIdentity identity,
        CancellationToken cancellationToken);
}
