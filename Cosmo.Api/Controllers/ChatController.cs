using Microsoft.AspNetCore.Mvc;

namespace Cosmo.Api.Controllers
{
    [ApiController]
    [Route("api/chat")]
    public class ChatController : ControllerBase
    {

        [HttpPost("sendMessage")]
        public async Task<IActionResult> SendMessage()
        {
            return Ok("test");
        }
    }
}
