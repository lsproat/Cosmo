using Cosmo.Application.Abstractions;
using Cosmo.Application.Exceptions;

namespace Cosmo.Api.Authentication;

public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public ExternalUserIdentity GetRequiredIdentity()
    {
        var principal = httpContextAccessor.HttpContext?.User;

        if (principal?.Identity?.IsAuthenticated != true)
            throw new UnauthenticatedUserException();

        var issuer = principal.FindFirst("iss")?.Value;
        var subject = principal.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(issuer) ||
            string.IsNullOrWhiteSpace(subject))
        {
            throw new UnauthenticatedUserException();
        }

        return new ExternalUserIdentity(issuer, subject);
    }
}
