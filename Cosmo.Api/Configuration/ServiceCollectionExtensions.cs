using Cosmo.Infrastructure.Configuration;

namespace Cosmo.Api.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCosmoConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<OllamaOptions>()
            .Bind(configuration.GetSection(OllamaOptions.SectionName))
            .Validate(
                options => Uri.TryCreate(
                    options.BaseUrl,
                    UriKind.Absolute,
                    out var uri)
                    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
                "Ollama:BaseUrl must be a valid absolute HTTP or HTTPS URL.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Model),
                "Ollama:Model must not be empty or whitespace.")
            .ValidateOnStart();

        services
            .AddOptions<CosmoOptions>()
            .Bind(configuration.GetSection(CosmoOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.BaseSystemPrompt),
                "Cosmo:BaseSystemPrompt must not be empty or whitespace.")
            .ValidateOnStart();

        return services;
    }
}
