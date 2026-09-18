namespace Cosmo.Infrastructure.LLMs.Ollama;

internal sealed record OllamaMessage(
    string Role,
    string Content);
