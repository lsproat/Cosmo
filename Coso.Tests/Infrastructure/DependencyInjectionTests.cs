using Cosmo.Api.Configuration;
using Cosmo.Application.Abstractions;
using Cosmo.Infrastructure;
using Cosmo.Infrastructure.Conversations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Cosmo.Tests.Infrastructure;

public class DependencyInjectionTests
{
    [Fact]
    public void Infrastructure_RepositoryAndContextAreSharedWithinScopeButNotAcrossRequests()
    {
        using var provider = CreateServices("Host=localhost;Database=unused;Username=unused");
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var repository = first.ServiceProvider.GetRequiredService<IConversationRepository>();
        Assert.IsType<EfConversationRepository>(repository);
        Assert.Same(repository, first.ServiceProvider.GetRequiredService<IConversationRepository>());
        Assert.NotSame(repository, second.ServiceProvider.GetRequiredService<IConversationRepository>());
        var context = first.ServiceProvider.GetRequiredService<CosmoDbContext>();
        Assert.Same(context, first.ServiceProvider.GetRequiredService<CosmoDbContext>());
        Assert.NotSame(context, second.ServiceProvider.GetRequiredService<CosmoDbContext>());
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
    }

    [Fact]
    public void Infrastructure_UsesValidatedConnectionOptions()
    {
        using var provider = CreateServices("");
        using var scope = provider.CreateScope();
        Assert.Throws<OptionsValidationException>(() => scope.ServiceProvider.GetRequiredService<CosmoDbContext>());
    }

    private static ServiceProvider CreateServices(string connectionString)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:CosmoDatabase"] = connectionString,
            ["Ollama:BaseUrl"] = "http://localhost:11434",
            ["Ollama:Model"] = "test-model",
            ["Ollama:ContextWindowTokens"] = "1000",
            ["Ollama:ReservedOutputTokens"] = "100",
            ["Cosmo:BaseSystemPrompt"] = "Test"
        }).Build();
        return new ServiceCollection().AddCosmoConfiguration(configuration).AddInfrastructure(configuration)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}
