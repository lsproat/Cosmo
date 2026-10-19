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
            .Validate(
                options => options.ContextWindowTokens > 0,
                "Ollama:ContextWindowTokens must not be empty and must be greater than 0.")
            .Validate(
                options => options.ReservedOutputTokens > 0,
                "Ollama:ReservedOutputTokens must not be empty and must be greater than 0.")
            .Validate(
                options => options.ReservedOutputTokens < options.ContextWindowTokens,
                "Ollama:ReservedOutputTokens must be less than Ollama:ContextWindowTokens.")
            .ValidateOnStart();

        services
            .AddOptions<CosmoOptions>()
            .Bind(configuration.GetSection(CosmoOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.BaseSystemPrompt),
                "Cosmo:BaseSystemPrompt must not be empty or whitespace.")
            .ValidateOnStart();

        services.AddOptions<ConnectionStringOptions>()
            .Bind(configuration.GetSection(ConnectionStringOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.CosmoDatabase),
                "ConnectionStrings:CosmoDatabase must not be empty or whitespace.")
            .ValidateOnStart();

        services.AddOptions<Auth0Options>()
            .Bind(configuration.GetSection(Auth0Options.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Authority),
                "Auth0:Authority must not be empty or whitespace.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Audience),
                "Auth0:Audience must not be empty or whitespace.")
            .ValidateOnStart();

        return services;
    }
}
