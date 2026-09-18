using Cosmo.Application.Chat;
using Cosmo.Application.Interfaces;
using Cosmo.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace Cosmo.Infrastructure.LLMs.Ollama;

public class OllamaModelProvider(
    HttpClient httpClient,
    IOptions<OllamaOptions> options) : IModelProvider
{
    private readonly OllamaOptions _options = options.Value;

    public async Task<SendMessageResult> SendMessageAsync(string message, CancellationToken cancellationToken)
    {
        var request = new OllamaChatRequest(
           Model: _options.Model,
           Messages:
           [
               new OllamaMessage("user", message)
           ],
           // Todo: Add support for streaming responses
           Stream: false);

        var response = await httpClient.PostAsJsonAsync(
            "/api/chat",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<OllamaChatResponse>(
                cancellationToken: cancellationToken);

        return new SendMessageResult(result!.Message.Content);
    }
}
