namespace Cosmo.Domain.Users;

public sealed class User
{
    public int Id { get; private set; }

    public string ExternalIssuer { get; private set; } = string.Empty;

    public string ExternalSubject { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }
        = DateTimeOffset.UtcNow;

    private User()
    { }

    public User(string externalIssuer, string externalSubject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalIssuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalSubject);

        ExternalIssuer = externalIssuer;
        ExternalSubject = externalSubject;
    }
}
