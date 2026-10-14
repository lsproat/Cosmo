namespace Cosmo.Application.Abstractions;

public sealed class ModelResponse(string content, int promptTokenCount)
{
    public string Content => content;

    public int PromptTokenCount => promptTokenCount;
}
