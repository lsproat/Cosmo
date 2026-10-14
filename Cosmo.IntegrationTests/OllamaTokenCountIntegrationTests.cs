using Cosmo.Domain.Conversations;
using Cosmo.Infrastructure.Tokenization.Gemma;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Cosmo.IntegrationTests;

public class OllamaTokenCountIntegrationTests(ITestOutputHelper output)
{
    [OllamaIntegrationTheory]
    [Trait("Category", "Integration")]
    [InlineData("short")]
    [InlineData("history")]
    [InlineData("whitespace")]
    [InlineData("unicode")]
    public async Task CountTokens_MatchesOllamaPromptEvalCount(string scenario)
    {
        var messages = CreateMessages(scenario);
        var counter = new Gemma4TokenCounter();
        var estimated = counter.CountTokens(messages);
        var model = Environment.GetEnvironmentVariable("COSMO_OLLAMA_MODEL") ?? "gemma4:26b";
        using var client = new HttpClient
        {
            BaseAddress = new Uri(Environment.GetEnvironmentVariable("COSMO_OLLAMA_BASE_URL")!),
            Timeout = TimeSpan.FromMinutes(3)
        };

        // One generated token is sufficient: this test checks the input count,
        // so it does not require a complete or nonempty assistant answer.
        using var response = await client.PostAsJsonAsync("/api/chat", new
        {
            model,
            messages = messages.Select(message => new
            {
                role = message.Role switch
                {
                    ConversationMessageRole.System => "system",
                    ConversationMessageRole.User => "user",
                    ConversationMessageRole.Assistant => "assistant",
                    _ => throw new ArgumentOutOfRangeException(nameof(message.Role))
                },
                content = message.Content
            }).ToArray(),
            stream = false,
            think = true,
            options = new { num_ctx = 4096, num_predict = 1 }
        });
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.GetProperty("done").GetBoolean());
        var actual = body.RootElement.GetProperty("prompt_eval_count").GetInt32();

        output.WriteLine($"Model={model}, scenario={scenario}, estimated={estimated}, actual={actual}, delta={actual - estimated}");
        Assert.True(actual > 0, "Ollama must return a positive prompt_eval_count.");
        Assert.True(estimated == actual,
            $"Model={model}, scenario={scenario}: local={estimated}, Ollama={actual}, delta={actual - estimated}. " +
            "Check the model tokenizer, chat renderer, thinking setting, and prompt truncation.\n" +
            counter.FormatGemma4Conversation(messages));
    }

    private static ConversationMessage[] CreateMessages(string scenario)
    {
        ConversationMessage system = new(ConversationMessageRole.System,
            "You are Cosmo. Answer clearly and briefly.\n\nCurrent date: September 23, 2026\nCurrent time: 7:48 PM");

        return scenario switch
        {
            "short" => [system, new(ConversationMessageRole.User, "Hi Cosmo. How's it going?")],
            "history" => [system,
                new(ConversationMessageRole.User, "What is two plus two?"),
                new(ConversationMessageRole.Assistant, "Two plus two is four."),
                new(ConversationMessageRole.User, "And four plus three?")],
            "whitespace" => [system,
                new(ConversationMessageRole.User, "  First line.\r\n\r\nSecond line.\tIndented text.  ")],
            "unicode" => [system,
                new(ConversationMessageRole.User, "Explain \"こんにちは\", café, and 🚀 in plain English.")],
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown scenario.")
        };
    }
}

public sealed class OllamaIntegrationTheoryAttribute : TheoryAttribute
{
    public OllamaIntegrationTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COSMO_OLLAMA_BASE_URL")))
        {
            Skip = "Set COSMO_OLLAMA_BASE_URL to run against a real Ollama server with gemma4:26b installed.";
        }
    }
}
