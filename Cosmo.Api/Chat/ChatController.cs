using Cosmo.Application.Chat.ContinueConversation;
using Cosmo.Application.Chat.CreateConversation;
using Cosmo.Application.Chat.GetAllConversations;
using Cosmo.Application.Chat.GetConversation;
using Cosmo.Application.Chat.SendMessage;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cosmo.Api.Chat;

[Authorize]
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
    public async Task<ActionResult<ContinueConversationResult>> ContinueConversation(int conversationId, ContinueConversationRequest request, CancellationToken cancellationToken)
    {
        var command = new ContinueConversationCommand(conversationId, request.Message);
        var result = await sender.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpGet("conversation/{conversationId}")]
    public async Task<ActionResult<GetConversationResult>> GetConversation(int conversationId, CancellationToken cancellationToken)
    {
        var query = new GetConversationQuery(conversationId);
        var result = await sender.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("conversations")]
    public async Task<ActionResult<GetAllConversationsResult>> GetConversations(CancellationToken cancellationToken)
    {
        var query = new GetAllConversationsQuery();
        var result = await sender.Send(query, cancellationToken);
        return Ok(result);
    }
}
