using Cosmo.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
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
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace Cosmo.Tests.Api;

public class ChatApiTests
{
    [Fact]
    public async Task Swagger_Development_DescribesOAuthAndConfiguresPkceAndAudience()
    {
        using var factory = new ChatApiFactory { EnvironmentName = "Development" };
        using var client = factory.CreateHttpsClient(null);
        using var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var scheme = json.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("Auth0");
        Assert.Equal("oauth2", scheme.GetProperty("type").GetString());
        var flow = scheme.GetProperty("flows").GetProperty("authorizationCode");
        Assert.Equal($"{TestAuthenticationHandler.Issuer}authorize", flow.GetProperty("authorizationUrl").GetString());
        Assert.Equal($"{TestAuthenticationHandler.Issuer}oauth/token", flow.GetProperty("tokenUrl").GetString());
        var security = json.RootElement.GetProperty("paths").GetProperty("/api/chat/conversations")
            .GetProperty("get").GetProperty("security");
        Assert.Contains(security.EnumerateArray(), item => item.TryGetProperty("Auth0", out _));

        var initializer = await client.GetStringAsync("/swagger/index.js");
        Assert.Contains("test-swagger-client", initializer);
        Assert.Contains("\"usePkceWithAuthorizationCodeGrant\":true", initializer);
        Assert.Contains("https://cosmo-test.invalid", initializer);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Swagger_OutsideDevelopment_IsNotExposedOrRequired(string environment)
    {
        using var factory = new ChatApiFactory { EnvironmentName = environment };
        using var client = factory.CreateHttpsClient(null);
        using var document = await client.GetAsync("/openapi/v1.json");
        using var ui = await client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.NotFound, document.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, ui.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    [InlineData("missing-subject")]
    public async Task GetConversations_WithoutValidUser_ReturnsUnauthorized(string? subject)
    {
        using var factory = new ChatApiFactory();
        using var client = factory.CreateHttpsClient(subject);
        using var response = await client.GetAsync("/api/chat/conversations");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(factory.Model.Calls);
        using var scope = factory.Services.CreateScope();
        Assert.Empty(scope.ServiceProvider.GetRequiredService<CosmoDbContext>().Users);
    }

    [Theory]
    [InlineData("/api/chat/sendMessage")]
    [InlineData("/api/chat/conversation")]
    [InlineData("/api/chat/conversation/1")]
    public async Task Post_WithoutAuthentication_DoesNotInvokeModel(string route)
    {
        using var factory = new ChatApiFactory();
        using var client = factory.CreateHttpsClient(null);
        using var response = await client.PostAsJsonAsync(route, new { message = "Hello" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(factory.Model.Calls);
    }

    [Fact]
    public async Task Conversations_AreIsolatedAcrossAuthenticatedUsers()
    {
        using var factory = new ChatApiFactory();
        using var alice = factory.CreateHttpsClient("alice");
        using var bob = factory.CreateHttpsClient("bob");

        // A caller-supplied owner cannot override the server's resolved identity.
        using var created = await alice.PostAsJsonAsync("/api/chat/conversation",
            new { message = "Alice's private question", userId = 999 });
        created.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = json.RootElement.GetProperty("conversationId").GetInt32();

        using var bobList = await bob.GetAsync("/api/chat/conversations");
        bobList.EnsureSuccessStatusCode();
        using var listJson = JsonDocument.Parse(await bobList.Content.ReadAsStringAsync());
        Assert.Empty(listJson.RootElement.GetProperty("conversations").EnumerateArray());

        using var deniedRead = await bob.GetAsync($"/api/chat/conversation/{id}");
        using var deniedWrite = await bob.PostAsJsonAsync($"/api/chat/conversation/{id}",
            new { message = "Unauthorized continuation" });
        Assert.Equal(HttpStatusCode.NotFound, deniedRead.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deniedWrite.StatusCode);
        Assert.Single(factory.Model.Calls);
        Assert.Equal(2, factory.Get(id)!.Messages.Count);

        using var ownerRead = await alice.GetAsync($"/api/chat/conversation/{id}");
        ownerRead.EnsureSuccessStatusCode();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CosmoDbContext>();
        var owner = Assert.Single(db.Users, user => user.ExternalSubject == "alice");
        Assert.Equal(owner.Id, factory.Get(id)!.UserId);
        Assert.Equal(2, db.Users.Count());
    }

    [Fact]
    public async Task GetConversations_EmptyDatabase_ReturnsEmptyArrayWithoutCallingModel()
    {
        using var factory = new ChatApiFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync("/api/chat/conversations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(json.RootElement.GetProperty("conversations").EnumerateArray());
        Assert.Empty(factory.Model.Calls);
    }

    [Fact]
    public async Task GetConversations_ReturnsSummariesWithoutMessageHistory()
    {
        using var factory = new ChatApiFactory();
        using var client = factory.CreateHttpsClient();
        var first = new Conversation("A long opening message that becomes a title");
        first.AddAssistantMessage("Private reply");
        var second = new Conversation("Short title");
        factory.Seed(first);
        factory.Seed(second);

        using var response = await client.GetAsync("/api/chat/conversations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var summaries = json.RootElement.GetProperty("conversations").EnumerateArray().ToArray();
        Assert.Equal(2, summaries.Length);
        foreach (var expected in new[] { first, second })
        {
            var summary = Assert.Single(summaries, item => item.GetProperty("id").GetInt32() == expected.Id);
            Assert.Equal(expected.Title, summary.GetProperty("title").GetString());
            Assert.Equal(expected.CreatedAt, summary.GetProperty("createdAt").GetDateTimeOffset());
            Assert.Equal(3, summary.EnumerateObject().Count());
        }
        Assert.Empty(factory.Model.Calls);
    }

    [Fact]
    public async Task GetConversation_AfterCreateAndContinue_ReturnsPersistedHistoryAndMetadata()
    {
        using var factory = new ChatApiFactory();
        using var client = factory.CreateHttpsClient();
        using var created = await client.PostAsJsonAsync("/api/chat/conversation", new { message = "First question" });
        created.EnsureSuccessStatusCode();
        using var createJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = createJson.RootElement.GetProperty("conversationId").GetInt32();
        using var continued = await client.PostAsJsonAsync($"/api/chat/conversation/{id}", new { message = "Next question" });
        continued.EnsureSuccessStatusCode();
        var stored = factory.Get(id)!;

        using var response = await client.GetAsync($"/api/chat/conversation/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var conversation = json.RootElement.GetProperty("conversation");
        Assert.Equal(id, conversation.GetProperty("id").GetInt32());
        Assert.Equal("First question", conversation.GetProperty("title").GetString());
        Assert.Equal(stored.CreatedAt, conversation.GetProperty("createdAt").GetDateTimeOffset());
        var messages = conversation.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal(new[] { "First question", "Model reply", "Next question", "Model reply" },
            messages.Select(message => message.GetProperty("content").GetString()));
        Assert.Equal(new[] { 0, 1, 2, 3 }, messages.Select(message => message.GetProperty("sequenceNumber").GetInt32()));
        Assert.Equal(new[] { 1, 2, 1, 2 }, messages.Select(message => message.GetProperty("role").GetInt32()));
        Assert.Equal(stored.Messages.Select(message => message.Id), messages.Select(message => message.GetProperty("id").GetInt32()));
        Assert.Equal(stored.Messages.Select(message => message.CreatedAt), messages.Select(message => message.GetProperty("createdAt").GetDateTimeOffset()));
        Assert.Equal(2, factory.Model.Calls.Count);
    }

    [Fact]
    public async Task GetConversation_UnknownId_ReturnsNotFoundProblemWithoutCallingModel()
    {
        using var factory = new ChatApiFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync($"/api/chat/conversation/{int.MaxValue}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(404, json.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("traceId").GetString()));
        Assert.Empty(factory.Model.Calls);
    }

    [Fact]
    public async Task GetConversation_InvalidInteger_ReturnsNotFoundWithoutCallingModel()
    {
        using var factory = new ChatApiFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync("/api/chat/conversation/not-an-integer");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(factory.Model.Calls);
    }

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
        factory.Seed(conversation);
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
        var id = createJson.RootElement.GetProperty("conversationId").GetInt32();
        Assert.True(id > 0);
        Assert.Equal("Model reply", createJson.RootElement.GetProperty("message").GetString());

        using var continued = await client.PostAsJsonAsync($"/api/chat/conversation/{id}", new { message = "Second" });

        Assert.Equal(HttpStatusCode.OK, continued.StatusCode);
        using var continueJson = JsonDocument.Parse(await continued.Content.ReadAsStringAsync());
        Assert.Equal("Model reply", continueJson.RootElement.GetProperty("message").GetString());
        Assert.False(continueJson.RootElement.TryGetProperty("conversationId", out _));
        Assert.Equal(ConversationMessageRole.System, factory.Model.Calls[0][0].Role);
        Assert.StartsWith("API test instructions", factory.Model.Calls[0][0].Content);
        Assert.Equal(new[] { "First", "Model reply", "Second" }, factory.Model.Calls[1].Skip(1).Select(x => x.Content));
        Assert.Equal(new[] { "First", "Model reply", "Second", "Model reply" }, factory.Get(id)!.Messages.Select(x => x.Content));
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
    public async Task ContinueConversation_InvalidInteger_ReturnsNotFound()
    {
        using var factory = new ChatApiFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.PostAsJsonAsync("/api/chat/conversation/not-an-integer", new { message = "Hello" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(factory.Model.Calls);
    }

    [Fact]
    public async Task ContinueConversation_UnknownId_ReturnsNotFound()
    {
        using var factory = new ChatApiFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.PostAsJsonAsync($"/api/chat/conversation/{int.MaxValue}", new { message = "Hello" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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

    [Fact]
    public async Task FailedContinuationThenRetry_PreservesHistoryWithoutDuplicateUserInput()
    {
        using var factory = new ChatApiFactory();
        using var client = factory.CreateHttpsClient();
        var conversation = new Conversation("First");
        conversation.AddAssistantMessage("First answer");
        factory.Seed(conversation);
        factory.Model.Respond = _ => throw new HttpRequestException("Unavailable");

        using var failed = await client.PostAsJsonAsync($"/api/chat/conversation/{conversation.Id}", new { message = "Pending" });
        Assert.Equal(HttpStatusCode.BadGateway, failed.StatusCode);
        Assert.Equal(new[] { "First", "First answer" }, factory.Get(conversation.Id)!.Messages.Select(message => message.Content));

        factory.Model.Respond = _ => Task.FromResult(new ModelResponse("Answer", 42));
        using var retried = await client.PostAsJsonAsync($"/api/chat/conversation/{conversation.Id}", new { message = "Pending" });
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        Assert.Equal(new[] { "First", "First answer", "Pending", "Answer" }, factory.Get(conversation.Id)!.Messages.Select(message => message.Content));
    }

    [Fact]
    public async Task Continuation_TrimsModelContextButKeepsFullStoredHistory()
    {
        using var factory = new ChatApiFactory { Counter = new StubTokenCounter(CountInput) };
        using var client = factory.CreateHttpsClient();
        var question = new string('q', 400);
        var answer = new string('a', 400);
        var pending = new string('p', 200);
        var conversation = new Conversation(question);
        conversation.AddAssistantMessage(answer);
        factory.Seed(conversation);

        using var response = await client.PostAsJsonAsync($"/api/chat/conversation/{conversation.Id}", new { message = pending });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var context = Assert.Single(factory.Model.Calls);
        Assert.Equal(2, context.Length);
        Assert.Equal(ConversationMessageRole.System, context[0].Role);
        Assert.Equal(pending, context[^1].Content);
        Assert.Equal(new[] { question, answer, pending, "Model reply" }, factory.Get(conversation.Id)!.Messages.Select(message => message.Content));
    }

    [Theory]
    [InlineData(890, HttpStatusCode.OK)]
    [InlineData(891, HttpStatusCode.InternalServerError)]
    public async Task CreateConversation_EnforcesInputBudgetBeforeCallingModel(int length, HttpStatusCode status)
    {
        using var factory = new ChatApiFactory { Counter = new StubTokenCounter(CountInput) };
        using var client = factory.CreateHttpsClient();

        using var response = await client.PostAsJsonAsync("/api/chat/conversation", new { message = new string('x', length) });

        Assert.Equal(status, response.StatusCode);
        if (status == HttpStatusCode.OK) Assert.Single(factory.Model.Calls);
        else Assert.Empty(factory.Model.Calls);
    }

    private static int CountInput(IReadOnlyList<ConversationMessage> messages) =>
        10 + messages.Where(message => message.Role != ConversationMessageRole.System).Sum(message => message.Content.Length);

    private sealed class ChatApiFactory : WebApplicationFactory<ChatController>
    {
        private readonly string _databaseName = Guid.NewGuid().ToString();
        public string EnvironmentName { get; init; } = "Testing";

        public void Seed(Conversation conversation, string subject = "alice")
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CosmoDbContext>();
            conversation.UserId = scope.ServiceProvider.GetRequiredService<IUserResolver>()
                .GetOrCreateUserIdAsync(new(TestAuthenticationHandler.Issuer, subject), CancellationToken.None)
                .GetAwaiter().GetResult();
            db.Conversations.Add(conversation);
            db.SaveChanges();
        }

        public Conversation? Get(int id)
        {
            using var scope = Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<CosmoDbContext>().Conversations
                .Include(c => c.Messages.OrderBy(m => m.SequenceNumber)).SingleOrDefault(c => c.Id == id);
        }

        public StubModelProvider Model { get; } = new();
        public ITokenCounter Counter { get; set; } = new StubTokenCounter();

        public HttpClient CreateHttpsClient(string? subject = "alice")
        {
            var client = CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });
            if (subject is not null)
                client.DefaultRequestHeaders.Add("X-Test-Subject", subject);
            return client;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(EnvironmentName);
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.Sources.Clear();
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Ollama:BaseUrl"] = "https://ollama.invalid",
                    ["Ollama:Model"] = "test-model",
                    ["Ollama:ContextWindowTokens"] = "1000",
                    ["Ollama:ReservedOutputTokens"] = "100",
                    ["ConnectionStrings:CosmoDatabase"] = "Host=localhost;Database=cosmo_test;Username=unused",
                    ["Cosmo:BaseSystemPrompt"] = "API test instructions",
                    ["Auth0:Authority"] = TestAuthenticationHandler.Issuer,
                    ["Auth0:Audience"] = "https://cosmo-test.invalid",
                    ["Swagger:ClientId"] = EnvironmentName == "Development" ? "test-swagger-client" : null
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName, _ => { });
                services.RemoveAll<IModelProvider>();
                services.AddSingleton<IModelProvider>(Model);
                services.RemoveAll<ICosmoDbContext>();
                services.RemoveAll<CosmoDbContext>();
                services.RemoveAll<DbContextOptions<CosmoDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<CosmoDbContext>>();
                services.AddDbContext<ICosmoDbContext, CosmoDbContext>(options => options.UseInMemoryDatabase(_databaseName));
                services.RemoveAll<ITokenCounter>();
                services.AddSingleton<ITokenCounter>(Counter);
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider());
            });
        }
    }
}
