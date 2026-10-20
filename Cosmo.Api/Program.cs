using Cosmo.Api.Authentication;
using Cosmo.Api.Configuration;
using Cosmo.Api.Exceptions;
using Cosmo.Application;
using Cosmo.Application.Abstractions;
using Cosmo.Application.Exceptions;
using Cosmo.Infrastructure;
using Cosmo.Infrastructure.Configuration;
using Cosmo.Infrastructure.Conversations;
using Cosmo.Infrastructure.ModelSources.Ollama;
using Cosmo.Infrastructure.Tokenization.Gemma;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration
    .AddJsonFile($"localsettings.json", optional: true)
    .AddEnvironmentVariables();

builder.Services.AddCosmoConfiguration(builder.Configuration, builder.Environment);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<Auth0Options>>((auth, options) =>
    {
        auth.Authority = options.Value.Authority;
        auth.Audience = options.Value.Audience;
        auth.MapInboundClaims = false;
    });
builder.Services.AddAuthorization();

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] =
            Activity.Current?.Id
            ?? context.HttpContext.TraceIdentifier;
    };
});
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddControllers();
if (builder.Environment.IsDevelopment())
{
    builder.Services.ConfigureSwagger();
}
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
// Todo: Important for multiple model support. Validate that the configured model is compatible with this counter at startup, or select a counter matching the model.
builder.Services.AddSingleton<ITokenCounter, Gemma4TokenCounter>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IConversationContextBuilder, ConversationContextBuilder>();
builder.Services.AddHttpClient<IModelProvider, OllamaModelProvider>((services, client) =>
{
    var options = services.GetRequiredService<IOptions<OllamaOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    // If local model is not loaded in memory, it may take a while to respond to the first request.
    client.Timeout = TimeSpan.FromMinutes(5);
});
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    var swagger = app.Services
    .GetRequiredService<IOptions<SwaggerOptions>>()
    .Value;
    var auth0 = app.Services
        .GetRequiredService<IOptions<Auth0Options>>()
        .Value;

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/openapi/v1.json",
            "Cosmo API v1");

        options.RoutePrefix = "swagger";

        options.OAuthClientId(swagger.ClientId);
        options.OAuthAppName("Cosmo Swagger");
        options.OAuthUsePkce();
        options.OAuthScopes("openid", "profile", "email");

        options.OAuthAdditionalQueryStringParams(
            new Dictionary<string, string>
            {
                ["audience"] = auth0.Audience
            });
    });
}

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = exception => exception switch
    {
        UnauthenticatedUserException => StatusCodes.Status401Unauthorized,

        KeyNotFoundException => StatusCodes.Status404NotFound,

        ModelProviderResponseException =>
            StatusCodes.Status502BadGateway,

        HttpRequestException =>
            StatusCodes.Status502BadGateway,

        OperationCanceledException
        {
            InnerException: TimeoutException
        } =>
            StatusCodes.Status504GatewayTimeout,

        _ => StatusCodes.Status500InternalServerError
    }
});
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
