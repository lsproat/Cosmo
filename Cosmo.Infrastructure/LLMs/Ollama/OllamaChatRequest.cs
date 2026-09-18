namespace Cosmo.Infrastructure.LLMs.Ollama;

internal sealed record OllamaChatRequest(
    string Model,
    IReadOnlyList<OllamaMessage> Messages,
    bool Stream);