using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cosmo.Tests.Support;

// Test-only identities; requests still execute authorization and user resolution.
internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string Issuer = "https://test.auth0.invalid/";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-Subject", out var subject))
            return Task.FromResult(AuthenticateResult.NoResult());

        if (subject.ToString() == "invalid")
            return Task.FromResult(AuthenticateResult.Fail("Invalid test credential."));

        var claims = new List<Claim> { new("iss", Issuer) };
        if (subject.ToString() != "missing-subject")
            claims.Add(new Claim("sub", subject.ToString()));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(principal, SchemeName)));
    }
}
