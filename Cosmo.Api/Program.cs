using Cosmo.Api.Exceptions;
using Cosmo.Application;
using Cosmo.Application.Abstractions;
using Cosmo.Application.Exceptions;
using Cosmo.Infrastructure.Configuration;
using Cosmo.Infrastructure.Conversations;
using Cosmo.Infrastructure.LLMs.Ollama;
using Microsoft.Extensions.Options;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration
    .AddJsonFile($"localsettings.json", optional: true)
    .AddEnvironmentVariables();

// Todo: Potentially pull out config validation into a separate class to keep Program.cs clean.
builder.Services
    .AddOptions<OllamaOptions>()
    .Bind(builder.Configuration.GetSection(OllamaOptions.SectionName))
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
builder.Services.AddOpenApi();
builder.Services.AddApplication();

builder.Services.AddSingleton<IConversationRepository, InMemoryConversationRepository>();
builder.Services.AddHttpClient<IModelProvider, OllamaModelProvider>((services, client) =>
{
    var options = services.GetRequiredService<IOptions<OllamaOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    // If local model is not loaded in memory, it may take a while to respond to the first request.
    client.Timeout = TimeSpan.FromMinutes(5);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = exception => exception switch
    {
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
