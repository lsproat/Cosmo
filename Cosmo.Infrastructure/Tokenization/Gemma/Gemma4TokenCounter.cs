using Cosmo.Application.Abstractions;
using Cosmo.Domain.Conversations;
using System.Text;
using Tokenizers.DotNet;

namespace Cosmo.Infrastructure.Tokenization.Gemma;

public class Gemma4TokenCounter : ITokenCounter
{
    private readonly Tokenizer _tokenizer;

    public Gemma4TokenCounter()
    {
        var tokenizerPath = Path.Combine(
            AppContext.BaseDirectory,
            "Tokenization",
            "Gemma",
            "gemma426b-tokenizer.json");
        _tokenizer = new Tokenizer(vocabPath: tokenizerPath);
    }

    public int CountTokens(IReadOnlyList<ConversationMessage> messages)
    {
        var formattedConversation = FormatGemma4Conversation(messages);

        var tokens = _tokenizer.Encode(formattedConversation)
            ?? throw new InvalidOperationException("Failed to encode text using the configured tokenizer.");

        return tokens.Length;
    }

    public string FormatGemma4Conversation(IReadOnlyList<ConversationMessage> messages)
    {
        var builder = new StringBuilder("<bos>");

        foreach (var message in messages)
        {
            var role = message.Role switch
            {
                ConversationMessageRole.System => "system",
                ConversationMessageRole.User => "user",
                ConversationMessageRole.Assistant => "model",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(message.Role),
                    message.Role,
                    "Unsupported conversation message role.")
            };

            builder.Append("<|turn>");
            builder.Append(role);
            builder.Append('\n');

            if (message.Role == ConversationMessageRole.System)
            {
                builder.Append("<|think|>\n");
            }

            builder.Append(message.Content.Trim());
            builder.Append("<turn|>\n");
        }

        // Generation prompt
        builder.Append("<|turn>model\n");

        return builder.ToString();
    }
}
