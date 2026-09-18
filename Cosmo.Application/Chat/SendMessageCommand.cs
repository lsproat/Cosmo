using Cosmo.Application.Interfaces;
using MediatR;

namespace Cosmo.Application.Chat;

public sealed record SendMessageCommand(string Message) : IRequest<SendMessageResult>
{
    public sealed class SendMessageCommandHandler(IModelProvider modelProvider) : IRequestHandler<SendMessageCommand, SendMessageResult>
    {
        public async Task<SendMessageResult> Handle(
            SendMessageCommand request,
            CancellationToken cancellationToken)
        {
            var response = await modelProvider.SendMessageAsync(
               request.Message,
               cancellationToken);

            return response;
        }
    }
}