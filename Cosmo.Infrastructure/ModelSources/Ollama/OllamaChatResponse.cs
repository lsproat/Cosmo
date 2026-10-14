using System.Text.Json.Serialization;

namespace Cosmo.Infrastructure.ModelSources.Ollama;

internal sealed class OllamaChatResponse(OllamaMessage message)
{
    public OllamaMessage Message { get; } = message;

    [JsonPropertyName("prompt_eval_count")]
    public int PromptTokenCount { get; init; }

    [JsonPropertyName("eval_count")]
    public int OutputTokenCount { get; init; }
}
