using Cosmo.Application.Interfaces;
using Cosmo.Infrastructure.Configuration;
using Cosmo.Infrastructure.LLMs;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration
    .AddJsonFile($"localsettings.json", optional: true)
    .AddEnvironmentVariables();

builder.Services
    .AddOptions<OllamaOptions>()
    .Bind(builder.Configuration.GetSection("Ollama"))
    .Validate(
        options => Uri.TryCreate(
            options.BaseUrl,
            UriKind.Absolute,
            out _),
        "Ollama:BaseUrl must be a valid absolute URL.")
    .ValidateOnStart();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddHttpClient<IModelProvider, OllamaModelProvider>((services, client) =>
{
    var options = services.GetRequiredService<IOptions<OllamaOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
