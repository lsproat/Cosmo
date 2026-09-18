using Cosmo.Application.Chat;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cosmo.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController(ISender sender) : ControllerBase
    {
        [HttpPost("sendMessage")]
        public async Task<ActionResult<SendMessageResult>> SendMessage(SendMessageCommand command, CancellationToken cancellationToken)
        {
            var result = await sender.Send(command, cancellationToken);
            return Ok(result);
        }
    }
}
