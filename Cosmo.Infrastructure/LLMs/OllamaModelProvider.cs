using Cosmo.Application.Interfaces;

namespace Cosmo.Infrastructure.LLMs;

public class OllamaModelProvider : IModelProvider
{
    private readonly HttpClient _httpClient;

    public OllamaModelProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
}
