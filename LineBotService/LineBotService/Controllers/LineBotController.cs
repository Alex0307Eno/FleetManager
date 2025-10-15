using Cars.Application.Services;
using Cars.Shared.Line;
using isRock.LineBot;
using LineBotService.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace LineBotService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LineBotController : ControllerBase
    {
        private readonly Bot _bot;
        private readonly DriverService _driverService;
        private readonly VehicleService _vehicleService;

        public LineBotController(
            IConfiguration cfg,
            DriverService driverService,
            VehicleService vehicleService)
        {
            _bot = new Bot(cfg["LineBot:ChannelAccessToken"]);
            _driverService = driverService;
            _vehicleService = vehicleService;
        }

        // 確保 JSON 為陣列
        private void SafeReplyMessageWithJSON(string replyToken, string json)
        {
            if (!json.TrimStart().StartsWith("[")) json = $"[{json}]";
            _bot.ReplyMessageWithJSON(replyToken, json);
        }

        private void SafePushMessageWithJSON(string toUserId, string json)
        {
            if (!json.TrimStart().StartsWith("[")) json = $"[{json}]";
            _bot.PushMessageWithJSON(toUserId, json);
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] object raw)
        {
            Console.WriteLine("Webhook Body:");

            var body = raw.ToString() ?? string.Empty;
            Console.WriteLine(body);
            var events = Utility.Parsing(body);

            foreach (var e in events.events)
            {
                var replyToken = e.replyToken;
                var userId = e.source?.userId ?? "";
                if (e.type == "message")
                {
                    var msg = e.message.text?.Trim() ?? "";
                    

                    // === 預約車輛 ===
                    if (msg.Contains("預約車輛"))
                    {
                        var json = ApplicantTemplate.BuildStep1(); // 選即時 or 預訂
                        if (!json.TrimStart().StartsWith("[")) json = $"[{json}]";
                        _bot.ReplyMessageWithJSON(replyToken, json);
                        continue;
                    }

                    // === 測試功能 ===
                    if (msg == "ping")
                    {
                        _bot.ReplyMessage(replyToken, "pong!");
                        continue;
                    }

                    // 其他訊息
                    _bot.ReplyMessage(replyToken, $"你說了：{msg}");
                    continue;
                }

                if (e.type == "postback")
                {
                    var data = e.postback.data;

                    // === 審核通過：選駕駛 ===
                    if (data.StartsWith("action=reviewApprove"))
                    {
                        var start = DateTime.Now.AddHours(1);
                        var end = DateTime.Now.AddHours(3);

                        var drivers = await _driverService.GetAvailableDriversAsync(start, end);
                        if (drivers.Count == 0)
                        {
                            _bot.ReplyMessage(replyToken, "⚠️ 沒有可用駕駛");
                            continue;
                        }

                        var flex = DriverTemplate.BuildDriverSelectBubble(123, drivers
                            .Select(x => ((int)((dynamic)x).DriverId, (string)((dynamic)x).DriverName))
                            .ToList());
                        SafeReplyMessageWithJSON(replyToken, flex);
                    }

                    // === 已選駕駛：選車輛 ===
                    else if (data.StartsWith("action=assignDriver"))
                    {
                        var start = DateTime.Now.AddHours(1);
                        var end = DateTime.Now.AddHours(3);

                        var cars = await _vehicleService.GetAvailableVehiclesAsync(start, end);
                        if (cars.Count == 0)
                        {
                            _bot.ReplyMessage(replyToken, "⚠️ 無可用車輛");
                            continue;
                        }

                        var flex = DriverTemplate.BuildCarSelectBubble(123, cars
                            .Select(x => ((int)((dynamic)x).VehicleId, (string)((dynamic)x).PlateNo))
                            .ToList());
                        SafeReplyMessageWithJSON(replyToken, flex);
                    }

                    // === 選完車輛：完成通知 ===
                    else if (data.StartsWith("action=assignVehicle"))
                    {
                        var msg = DriverTemplate.BuildDoneBubble("王小明", "ABC-1234");
                        SafeReplyMessageWithJSON(replyToken, msg);
                    }
                }
            }

            return Ok();
        }
    }
}
