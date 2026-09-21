using System.Net;
using System.Text;
using System.Text.Json;
using Cosmo.Application.Exceptions;
using Cosmo.Domain.Conversations;
using Cosmo.Infrastructure.Configuration;
using Cosmo.Infrastructure.LLMs.Ollama;
using Microsoft.Extensions.Options;

namespace Cosmo.Tests.Infrastructure;

public class OllamaModelProviderTests
{
    [Fact]
    public async Task SendMessage_PostsConfiguredModelAndOrderedRolesWithoutStreaming()
    {
        using var transport = new StubHttpHandler(async (request, token) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://ollama.invalid/api/chat", request.RequestUri!.AbsoluteUri);
            Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);
            using var body = JsonDocument.Parse(await request.Content.ReadAsStringAsync(token));
            Assert.Equal("test-model:small", body.RootElement.GetProperty("model").GetString());
            Assert.False(body.RootElement.GetProperty("stream").GetBoolean());
            var messages = body.RootElement.GetProperty("messages").EnumerateArray().ToArray();
            Assert.Equal(new[] { "system", "user", "assistant" }, messages.Select(x => x.GetProperty("role").GetString()));
            Assert.Equal(new[] { "Instructions", "Question \"quoted\"\nこんにちは", "Prior answer" }, messages.Select(x => x.GetProperty("content").GetString()));
            return JsonResponse("""{"message":{"role":"assistant","content":"  Answer\nこんにちは  "}}""");
        });
        using var client = CreateClient(transport);

        var response = await CreateProvider(client).SendMessageAsync([
            new(ConversationMessageRole.System, "Instructions"),
            new(ConversationMessageRole.User, "Question \"quoted\"\nこんにちは"),
            new(ConversationMessageRole.Assistant, "Prior answer")], CancellationToken.None);

        Assert.Equal("  Answer\nこんにちは  ", response.Content);
        Assert.Equal(1, transport.Calls);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task SendMessage_NonSuccessStatus_ThrowsHttpRequestException(int status)
    {
        using var transport = new StubHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)));
        using var client = CreateClient(transport);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => CreateProvider(client).SendMessageAsync([], CancellationToken.None));

        Assert.Equal((HttpStatusCode)status, exception.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"message\":")]
    [InlineData("{\"message\":{\"content\":42}}")]
    public async Task SendMessage_InvalidJson_WrapsJsonException(string body)
    {
        using var transport = new StubHttpHandler((_, _) => Task.FromResult(JsonResponse(body)));
        using var client = CreateClient(transport);

        var exception = await Assert.ThrowsAsync<ModelProviderResponseException>(() => CreateProvider(client).SendMessageAsync([], CancellationToken.None));

        Assert.IsType<JsonException>(exception.InnerException);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"message\":null}")]
    [InlineData("{\"message\":{}}")]
    [InlineData("{\"message\":{\"content\":null}}")]
    [InlineData("{\"message\":{\"content\":\"\"}}")]
    [InlineData("{\"message\":{\"content\":\" \\t\\n\"}}")]
    public async Task SendMessage_MissingOrBlankResponseContent_ThrowsProviderResponseException(string body)
    {
        using var transport = new StubHttpHandler((_, _) => Task.FromResult(JsonResponse(body)));
        using var client = CreateClient(transport);

        var exception = await Assert.ThrowsAsync<ModelProviderResponseException>(() => CreateProvider(client).SendMessageAsync([], CancellationToken.None));

        Assert.Null(exception.InnerException);
    }

    [Fact]
    public async Task SendMessage_UnsupportedRole_ThrowsBeforeSendingHttpRequest()
    {
        using var transport = new StubHttpHandler((_, _) => throw new InvalidOperationException("Must not send"));
        using var client = CreateClient(transport);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => CreateProvider(client).SendMessageAsync(
            [new((ConversationMessageRole)999, "Invalid")], CancellationToken.None));

        Assert.Equal(0, transport.Calls);
    }

    [Fact]
    public async Task SendMessage_CallerCancellation_ReachesHttpTransport()
    {
        using var cancellation = new CancellationTokenSource();
        using var transport = new StubHttpHandler((_, token) =>
        {
            // HttpClient supplies a linked token, so test cancellation behavior, not token identity.
            Assert.True(token.CanBeCanceled);
            Assert.False(token.IsCancellationRequested);
            cancellation.Cancel();
            Assert.True(token.IsCancellationRequested);
            token.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Cancellation was not propagated.");
        });
        using var client = CreateClient(transport);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateProvider(client).SendMessageAsync([], cancellation.Token));

        Assert.Equal(1, transport.Calls);
    }

    [Fact]
    public async Task SendMessage_TransportFailure_PropagatesOriginalException()
    {
        var failure = new HttpRequestException("Transport unavailable");
        using var transport = new StubHttpHandler((_, _) => Task.FromException<HttpResponseMessage>(failure));
        using var client = CreateClient(transport);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => CreateProvider(client).SendMessageAsync([], CancellationToken.None));

        Assert.Same(failure, exception);
    }

    private static HttpClient CreateClient(HttpMessageHandler transport) => new(transport)
    {
        BaseAddress = new Uri("https://ollama.invalid/ignored/base/path/")
    };

    private static OllamaModelProvider CreateProvider(HttpClient client) => new(client, Options.Create(new OllamaOptions
    {
        BaseUrl = "https://ollama.invalid", Model = "test-model:small"
    }));

    private static HttpResponseMessage JsonResponse(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed class StubHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return send(request, cancellationToken);
        }
    }
}
