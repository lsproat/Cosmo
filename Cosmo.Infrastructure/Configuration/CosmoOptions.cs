namespace Cosmo.Infrastructure.Configuration;

public sealed class CosmoOptions
{
    public const string SectionName = "Cosmo";

    public required string BaseSystemPrompt { get; init; }
}