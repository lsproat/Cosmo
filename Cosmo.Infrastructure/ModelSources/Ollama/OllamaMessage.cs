namespace Cosmo.Infrastructure.ModelSources.Ollama;

internal sealed record OllamaMessage(
    string Role,
    string Content);
