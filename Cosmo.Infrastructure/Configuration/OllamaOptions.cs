namespace Cosmo.Infrastructure.Configuration;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    public required string BaseUrl { get; init; }

    public required string Model { get; init; }

    public required int ContextWindowTokens { get; init; }

    public required int ReservedOutputTokens { get; init; }
}
