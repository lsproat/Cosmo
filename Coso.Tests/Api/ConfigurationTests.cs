using Cosmo.Api.Configuration;
using Cosmo.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Cosmo.Tests.Support;

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
    [InlineData("ConnectionStrings:CosmoDatabase", null)]
    [InlineData("ConnectionStrings:CosmoDatabase", "")]
    [InlineData("ConnectionStrings:CosmoDatabase", " \t\n")]
    [InlineData("Auth0:Authority", null)]
    [InlineData("Auth0:Authority", "")]
    [InlineData("Auth0:Authority", " \t\n")]
    [InlineData("Auth0:Audience", null)]
    [InlineData("Auth0:Audience", "")]
    [InlineData("Auth0:Audience", " \t\n")]
    public void Configuration_MissingOrBlankRequiredValue_FailsAtStartup(string key, string? value)
    {
        using var services = BuildServices(key, value);

        var exception = Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IStartupValidator>().Validate());

        Assert.Contains(exception.Failures, x => x.Contains(key));
    }

    [Theory]
    [InlineData("Ollama:ContextWindowTokens", "0")]
    [InlineData("Ollama:ContextWindowTokens", "-1")]
    [InlineData("Ollama:ReservedOutputTokens", "0")]
    [InlineData("Ollama:ReservedOutputTokens", "-1")]
    [InlineData("Ollama:ReservedOutputTokens", "1000")]
    [InlineData("Ollama:ReservedOutputTokens", "1001")]
    public void Configuration_InvalidBudget_FailsAtStartup(string key, string value)
    {
        using var services = BuildServices(key, value);
        var exception = Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IStartupValidator>().Validate());
        Assert.Contains(exception.Failures, failure => failure.Contains(key));
    }

    [Theory]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    public void SwaggerClientId_IsRequiredOnlyInDevelopment(string environment, bool required)
    {
        using var services = BuildServices("Swagger:ClientId", null, environment);
        var validate = () => services.GetRequiredService<IStartupValidator>().Validate();
        if (required)
        {
            var exception = Assert.Throws<OptionsValidationException>(validate);
            Assert.Contains(exception.Failures, failure => failure.Contains("Swagger:ClientId"));
        }
        else
            validate();
    }

    [Fact]
    public void SwaggerClientId_ValidDevelopmentSettingsPassStartupValidation()
    {
        using var services = BuildServices("Swagger:ClientId", "test-client", "Development");
        services.GetRequiredService<IStartupValidator>().Validate();
        Assert.Equal("test-client", services.GetRequiredService<IOptions<SwaggerOptions>>().Value.ClientId);
    }

    private static ServiceProvider BuildServices(string key, string? value, string environment = "Production")
    {
        var values = new Dictionary<string, string?>
        {
            ["Ollama:BaseUrl"] = "https://ollama.invalid",
            ["Ollama:Model"] = "test-model",
            ["Ollama:ContextWindowTokens"] = "1000",
            ["Ollama:ReservedOutputTokens"] = "100",
            ["ConnectionStrings:CosmoDatabase"] = "Host=localhost;Database=cosmo_test;Username=unused",
            ["Cosmo:BaseSystemPrompt"] = "Test instructions",
            ["Auth0:Authority"] = "https://test.auth0.invalid/",
            ["Auth0:Audience"] = "https://cosmo-test.invalid"
        };
        if (value is null) values.Remove(key);
        else values[key] = value;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new ServiceCollection().AddCosmoConfiguration(configuration, new TestHostEnvironment(environment)).BuildServiceProvider();
    }
}
