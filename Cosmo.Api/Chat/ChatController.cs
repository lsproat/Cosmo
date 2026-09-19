using Cosmo.Application.Chat.ContinueConversation;
using Cosmo.Application.Chat.CreateConversation;
using Cosmo.Application.Chat.SendMessage;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cosmo.Api.Chat;

// Todo: Auth
[ApiController]
[Route("api/chat")]
public class ChatController(ISender sender) : ControllerBase
{
    [HttpPost("sendMessage")]
    [RequestSizeLimit(512 * 1024)]
    public async Task<ActionResult<SendMessageResult>> SendMessage(SendMessageRequest request, CancellationToken cancellationToken)
    {
        var command = new SendMessageCommand(request.Message);
        var result = await sender.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("conversation")]
    [RequestSizeLimit(512 * 1024)]
    public async Task<ActionResult<CreateConversationResult>> CreateConversation(CreateConversationRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateConversationCommand(request.Message);
        var result = await sender.Send(command, cancellationToken);
        return Ok(result);
    }


    [HttpPost("conversation/{conversationId}")]
    [RequestSizeLimit(512 * 1024)]
    public async Task<ActionResult<ContinueConversationResult>> ContinueConversation(Guid conversationId, ContinueConversationRequest request, CancellationToken cancellationToken)
    {
        var command = new ContinueConversationCommand(conversationId, request.Message);
        var result = await sender.Send(command, cancellationToken);
        return Ok(result);
    }
}
