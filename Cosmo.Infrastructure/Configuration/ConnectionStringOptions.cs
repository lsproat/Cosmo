namespace Cosmo.Infrastructure.Configuration;

public sealed class ConnectionStringOptions
{
    public const string SectionName = "ConnectionStrings";

    public required string CosmoDatabase { get; init; }
}
