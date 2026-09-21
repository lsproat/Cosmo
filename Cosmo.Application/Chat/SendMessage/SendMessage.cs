using Cosmo.Application.Abstractions;
using Cosmo.Domain.Conversations;
using MediatR;

namespace Cosmo.Application.Chat.SendMessage;

public record SendMessageCommand(string Message) : IRequest<SendMessageResult>;
public sealed class SendMessageHandler(IModelProvider modelProvider) : IRequestHandler<SendMessageCommand, SendMessageResult>
{
    public async Task<SendMessageResult> Handle(
        SendMessageCommand request,
        CancellationToken cancellationToken)
    {
        var response = await modelProvider.SendMessageAsync(
           [new ConversationMessage(
               ConversationMessageRole.User,
               request.Message)],
           cancellationToken);

        return new SendMessageResult(response.Content);
    }
}