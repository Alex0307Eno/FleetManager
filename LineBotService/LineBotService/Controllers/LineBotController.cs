using Cars.Data;
using Cars.Application.Services;
using Cars.Shared.Dtos.Line;
using isRock.LineBot;
using Microsoft.AspNetCore.Mvc;
using LineBotService.Handlers;

namespace LineBotService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LineBotController : ControllerBase
    {
        private readonly MessageHandler _messageHandler;
        private readonly PostbackHandler _postbackHandler;

        public LineBotController(
            MessageHandler messageHandler,
            PostbackHandler postbackHandler)
        {
            _messageHandler = messageHandler;
            _postbackHandler = postbackHandler;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] object raw)
        {
            var body = raw?.ToString() ?? string.Empty;
            Console.WriteLine("Webhook Body:\n" + body);

            var events = Utility.Parsing(body);
            foreach (var e in events.events)
            {
                var replyToken = e.replyToken;
                var userId = e.source?.userId ?? "";
                if (string.IsNullOrEmpty(userId)) continue;

                if (e.type == "message")
                    await _messageHandler.HandleMessageAsync(e, replyToken, userId);

                else if (e.type == "postback")
                    await _postbackHandler.HandlePostbackAsync(e, replyToken, userId);
            }

            return Ok();
        }
    }
}
