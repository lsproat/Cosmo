using Cosmo.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace Cosmo.Api.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCosmoConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
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

        if (environment.IsDevelopment())
        {
            services.AddOptions<SwaggerOptions>()
                .Bind(configuration.GetSection(SwaggerOptions.SectionName))
                .Validate(
                    options => !string.IsNullOrWhiteSpace(options.ClientId),
                    "Swagger:ClientId must not be empty or whitespace.")
                .ValidateOnStart();
        }

        return services;
    }

    public static IServiceCollection ConfigureSwagger(
        this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer(
                (document, context, cancellationToken) =>
                {
                    var auth0 = context.ApplicationServices
                        .GetRequiredService<IOptions<Auth0Options>>()
                        .Value;

                    var authority = auth0.Authority.TrimEnd('/');
                    document.Components ??= new OpenApiComponents();
                    document.Components.SecuritySchemes ??=
                        new Dictionary<string, IOpenApiSecurityScheme>();

                    document.Components.SecuritySchemes["Auth0"] =
                        new OpenApiSecurityScheme
                        {
                            Type = SecuritySchemeType.OAuth2,
                            Description = "Sign in to Cosmo through Auth0.",
                            Flows = new OpenApiOAuthFlows
                            {
                                AuthorizationCode = new OpenApiOAuthFlow
                                {
                                    AuthorizationUrl =
                                        new Uri($"{authority}/authorize"),

                                    TokenUrl =
                                        new Uri($"{authority}/oauth/token"),

                                    Scopes = new Dictionary<string, string>
                                    {
                                        ["openid"] = "Identify the signed-in user",
                                        ["profile"] = "Read basic profile information",
                                        ["email"] = "Read email information"
                                    }
                                }
                            }
                        };

                    // All current chat endpoints require authentication.
                    foreach (var path in document.Paths.Values)
                    {
                        if (path.Operations is null)
                            continue;

                        foreach (var operation in path.Operations.Values)
                        {
                            operation.Security ??= [];

                            operation.Security.Add(new OpenApiSecurityRequirement
                            {
                                [new OpenApiSecuritySchemeReference(
                                    "Auth0", document)] = []
                            });
                        }
                    }

                    return Task.CompletedTask;
                });
        });

        return services;
    }
}
