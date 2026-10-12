namespace Cosmo.Infrastructure.ModelSources.Ollama;

internal sealed record OllamaChatRequest(
    string Model,
    IReadOnlyList<OllamaMessage> Messages,
    bool Stream);
