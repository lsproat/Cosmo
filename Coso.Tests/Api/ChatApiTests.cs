using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Cosmo.Api.Chat;
using Cosmo.Application.Abstractions;
using Cosmo.Application.Exceptions;
using Cosmo.Domain.Conversations;
using Cosmo.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Cosmo.Tests.Api;

public class ChatApiTests
{
    [Theory]
    [InlineData("/api/chat/sendMessage", "")]
    [InlineData("/api/chat/sendMessage", " \t")]
    [InlineData("/api/chat/conversation", "")]
    [InlineData("/api/chat/conversation", " \t")]
    public async Task EmptyOrWhitespaceMessage_IsAcceptedByCurrentRules(string route, string message)
    {
        using var factory = new ChatApiFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.PostAsJsonAsync(route, new { message });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(message, Assert.Single(factory.Model.Calls)[^1].Content);
    }

    [Theory]
    [InlineData("/api/chat/sendMessage")]
    [InlineData("/api/chat/conversation")]
    public async Task MessageAtMaximumLength_IsAccepted(string route)
    {
        using var factory = new ChatApiFactory();
        using var client = factory.CreateHttpsClient();
        var message = new string('x', 64_000);

        using var response = await client.PostAsJsonAsync(route, new { message });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(message, Assert.Single(factory.Model.Calls)[^1].Content);
    }

    [Fact]
    public async Task ContinueConversation_HasNoApplicationMessageLengthValidator()
    {
        using var factory = new ChatApiFactory();
        using var client = factory.CreateHttpsClient();
        var conversation = new Conversation("First");
        factory.Services.GetRequiredService<IConversationRepository>().Add(conversation);
        var message = new string('x', 64_001);

        using var response = await client.PostAsJsonAsync($"/api/chat/conversation/{conversation.Id}", new { message });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(message, Assert.Single(factory.Model.Calls)[^1].Content);
    }

    [Fact]
    public async Task SendMessage_ReturnsMessageAndSendsStatelessUserContext()
    {
        using var factory = new ChatApiFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.PostAsJsonAsync("/api/chat/sendMessage", new { message = "Hello" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Model reply", json.RootElement.GetProperty("message").GetString());
        Assert.False(json.RootElement.TryGetProperty("conversationId", out _));
        var message = Assert.Single(Assert.Single(factory.Model.Calls));
        Assert.Equal("Hello", message.Content);
        Assert.Equal(ConversationMessageRole.User, message.Role);
    }

    [Fact]
    public async Task CreateThenContinueConversation_PreservesIdAndHistoryAcrossRequests()
    {
        using var factory = new ChatApiFactory();
        using var client = factory.CreateHttpsClient();
        using var created = await client.PostAsJsonAsync("/api/chat/conversation", new { message = "First" });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var createJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = createJson.RootElement.GetProperty("conversationId").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal("Model reply", createJson.RootElement.GetProperty("message").GetString());

        using var continued = await client.PostAsJsonAsync($"/api/chat/conversation/{id}", new { message = "Second" });

        Assert.Equal(HttpStatusCode.OK, continued.StatusCode);
        using var continueJson = JsonDocument.Parse(await continued.Content.ReadAsStringAsync());
        Assert.Equal("Model reply", continueJson.RootElement.GetProperty("message").GetString());
        Assert.False(continueJson.RootElement.TryGetProperty("conversationId", out _));
        Assert.Equal(ConversationMessageRole.System, factory.Model.Calls[0][0].Role);
        Assert.StartsWith("API test instructions", factory.Model.Calls[0][0].Content);
        Assert.Equal(new[] { "First", "Model reply", "Second" }, factory.Model.Calls[1].Skip(1).Select(x => x.Content));
        var repository = factory.Services.GetRequiredService<IConversationRepository>();
        Assert.Equal(new[] { "First", "Model reply", "Second", "Model reply" }, repository.Get(id)!.Messages.Select(x => x.Content));
    }

    [Theory]
    [InlineData("/api/chat/sendMessage")]
    [InlineData("/api/chat/conversation")]
    public async Task OverlongMessage_ReturnsValidationProblemWithoutCallingModel(string route)
    {
        using var factory = new ChatApiFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.PostAsJsonAsync(route, new { message = new string('x', 64_001) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, json.RootElement.GetProperty("status").GetInt32());
        Assert.NotEmpty(json.RootElement.GetProperty("errors").GetProperty("Message").EnumerateArray());
        Assert.True(json.RootElement.TryGetProperty("traceId", out _));
        Assert.Empty(factory.Model.Calls);
    }

    [Theory]
    [InlineData("/api/chat/sendMessage", "{}")]
    [InlineData("/api/chat/sendMessage", "{\"message\":null}")]
    [InlineData("/api/chat/conversation", "{}")]
    [InlineData("/api/chat/conversation", "{\"message\":null}")]
    [InlineData("/api/chat/sendMessage", "{broken")]
    public async Task InvalidRequestBody_ReturnsBadRequestWithoutCallingModel(string route, string body)
    {
        using var factory = new ChatApiFactory();
        using var client = factory.CreateHttpsClient();
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(route, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.Model.Calls);
    }

    [Fact]
    public async Task ContinueConversation_InvalidGuid_ReturnsBadRequest()
    {
        using var factory = new ChatApiFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.PostAsJsonAsync("/api/chat/conversation/not-a-guid", new { message = "Hello" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.Model.Calls);
    }

    [Fact]
    public async Task ContinueConversation_UnknownId_CurrentlyReturnsInternalServerError()
    {
        using var factory = new ChatApiFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.PostAsJsonAsync($"/api/chat/conversation/{Guid.NewGuid()}", new { message = "Hello" });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(factory.Model.Calls);
    }

    [Theory]
    [InlineData("invalid-response", 502)]
    [InlineData("http-error", 502)]
    [InlineData("timeout", 504)]
    [InlineData("unexpected", 500)]
    public async Task ProviderFailure_ReturnsConfiguredProblemStatus(string failure, int expectedStatus)
    {
        using var factory = new ChatApiFactory();
        factory.Model.Respond = _ => Task.FromException<ModelResponse>(failure switch
        {
            "invalid-response" => new ModelProviderResponseException("Invalid model response"),
            "http-error" => new HttpRequestException("Backend unavailable"),
            "timeout" => new OperationCanceledException("Timed out", new TimeoutException()),
            _ => new InvalidOperationException("Unexpected failure")
        });
        using var client = factory.CreateHttpsClient();

        using var response = await client.PostAsJsonAsync("/api/chat/sendMessage", new { message = "Hello" });

        Assert.Equal((HttpStatusCode)expectedStatus, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedStatus, json.RootElement.GetProperty("status").GetInt32());
        Assert.True(json.RootElement.TryGetProperty("traceId", out _));
    }

    private sealed class ChatApiFactory : WebApplicationFactory<ChatController>
    {
        public StubModelProvider Model { get; } = new();

        public HttpClient CreateHttpsClient() => CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.Sources.Clear();
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Ollama:BaseUrl"] = "https://ollama.invalid",
                    ["Ollama:Model"] = "test-model",
                    ["Cosmo:BaseSystemPrompt"] = "API test instructions"
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IModelProvider>();
                services.AddSingleton<IModelProvider>(Model);
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider());
            });
        }
    }
}
