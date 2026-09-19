using Cosmo.Application.Abstractions;
using Cosmo.Application.Exceptions;
using Cosmo.Domain.Conversations;
using Cosmo.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cosmo.Infrastructure.LLMs.Ollama;

public class OllamaModelProvider(
    HttpClient httpClient,
    IOptions<OllamaOptions> options) : IModelProvider
{
    private readonly OllamaOptions _options = options.Value;

    public async Task<ModelResponse> SendMessageAsync(
        IReadOnlyList<ConversationMessage> messages,
        CancellationToken cancellationToken)
    {
        var request = new OllamaChatRequest(
           Model: _options.Model,
           Messages: [.. messages.Select(x => 
                        new OllamaMessage(GetOllamaRole(x.Role), x.Content))],
           // Todo: Add support for streaming responses
           Stream: false);

        using var response = await httpClient.PostAsJsonAsync(
            "/api/chat",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        OllamaChatResponse? result;
        try
        {
            result = await response.Content
                .ReadFromJsonAsync<OllamaChatResponse>(
                    cancellationToken: cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new ModelProviderResponseException(
                "Ollama returned an empty or invalid JSON response.",
                exception);
        }

        var content = result?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ModelProviderResponseException(
                "Ollama returned no message content.");
        }

        return new ModelResponse(content);
    }

    private static string GetOllamaRole(ConversationMessageRole role) =>
    role switch
    {
        ConversationMessageRole.System => "system",
        ConversationMessageRole.User => "user",
        ConversationMessageRole.Assistant => "assistant",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };
}
