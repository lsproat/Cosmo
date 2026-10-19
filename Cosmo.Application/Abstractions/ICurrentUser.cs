namespace Cosmo.Application.Abstractions;

public interface ICurrentUser
{
    ExternalUserIdentity GetRequiredIdentity();
}
