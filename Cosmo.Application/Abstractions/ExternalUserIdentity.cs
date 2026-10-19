namespace Cosmo.Application.Abstractions;

public sealed record ExternalUserIdentity(
    string Issuer,
    string Subject);
