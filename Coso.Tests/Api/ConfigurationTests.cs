using Cosmo.Api.Configuration;
using Cosmo.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Cosmo.Tests.Api;

public class ConfigurationTests
{
    [Theory]
    [InlineData("http://localhost:11434")]
    [InlineData("https://model.example/api/")]
    public void Configuration_BindsValidSettingsAndPassesStartupValidation(string url)
    {
        using var services = BuildServices("Ollama:BaseUrl", url);

        services.GetRequiredService<IStartupValidator>().Validate();

        var ollama = services.GetRequiredService<IOptions<OllamaOptions>>().Value;
        Assert.Equal(url, ollama.BaseUrl);
        Assert.Equal("test-model", ollama.Model);
        Assert.Equal("Test instructions", services.GetRequiredService<IOptions<CosmoOptions>>().Value.BaseSystemPrompt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("localhost:11434")]
    [InlineData("/relative/path")]
    [InlineData("ftp://model.example")]
    [InlineData("not a url")]
    public void Configuration_InvalidBaseUrl_FailsAtStartup(string? value)
    {
        using var services = BuildServices("Ollama:BaseUrl", value);

        var exception = Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IStartupValidator>().Validate());

        Assert.Contains(exception.Failures, x => x.Contains("Ollama:BaseUrl"));
    }

    [Theory]
    [InlineData("Ollama:Model", null)]
    [InlineData("Ollama:Model", "")]
    [InlineData("Ollama:Model", " \t\n")]
    [InlineData("Cosmo:BaseSystemPrompt", null)]
    [InlineData("Cosmo:BaseSystemPrompt", "")]
    [InlineData("Cosmo:BaseSystemPrompt", " \t\n")]
    public void Configuration_MissingOrBlankRequiredValue_FailsAtStartup(string key, string? value)
    {
        using var services = BuildServices(key, value);

        var exception = Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IStartupValidator>().Validate());

        Assert.Contains(exception.Failures, x => x.Contains(key));
    }

    private static ServiceProvider BuildServices(string key, string? value)
    {
        var values = new Dictionary<string, string?>
        {
            ["Ollama:BaseUrl"] = "https://ollama.invalid",
            ["Ollama:Model"] = "test-model",
            ["Cosmo:BaseSystemPrompt"] = "Test instructions"
        };
        if (value is null) values.Remove(key);
        else values[key] = value;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new ServiceCollection().AddCosmoConfiguration(configuration).BuildServiceProvider();
    }
}
