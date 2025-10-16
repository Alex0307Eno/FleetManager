using BCrypt.Net;
using Cars.Application.Services;
using Cars.Data;
using Cars.Shared.Dtos.Line;
using Cars.Shared.Line;
using isRock.LineBot;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace LineBotService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LineBotController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly Bot _bot;
        private readonly DriverService _driverService;
        private readonly VehicleService _vehicleService;
        private readonly CarApplicationService _carApplicationService;

        // 暫存使用者對話狀態
        private static readonly Dictionary<string, BookingStateDto> _conversationStore = new();

        public LineBotController(IConfiguration cfg, DriverService driverService, VehicleService vehicleService, CarApplicationService carApplicationService, ApplicationDbContext db )
        {
            _bot = new Bot(cfg["LineBot:ChannelAccessToken"]);
            _driverService = driverService;
            _vehicleService = vehicleService;
            _carApplicationService = carApplicationService;
            _db = db;
        }
        // 安全地回覆 Flex Message
        private void SafeReply(string replyToken, string json)
        {
            try
            {
                // 若不是陣列，就自動包成陣列格式
                if (!json.TrimStart().StartsWith("["))
                    json = "[" + json + "]";

                _bot.ReplyMessageWithJSON(replyToken, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Flex ERROR] {ex.Message}");
            }
        }
        // Push 訊息給使用者
        private void SafePush(string userId, string json)
        {
            try
            {
                Console.WriteLine($"[DEBUG] 推送給 {userId}");
                Console.WriteLine($"[DEBUG] JSON: {json}");
                _bot.PushMessageWithJSON(userId, json);
                Console.WriteLine("[DEBUG] 推送成功");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Push ERROR] {ex.Message}");
            }
        }






        [HttpPost]
        public async Task<IActionResult> Post([FromBody] object raw)
        {
            var body = raw.ToString() ?? string.Empty;
            Console.WriteLine("Webhook Body:");
            Console.WriteLine(body);

            var events = Utility.Parsing(body);
            foreach (var e in events.events)
            {
                var replyToken = e.replyToken;
                var userId = e.source?.userId ?? "";
                if (string.IsNullOrEmpty(userId)) continue;

                // === 一般訊息 ===
                if (e.type == "message")
                {
                    var msg = e.message.text?.Trim() ?? "";

                    // 🔹 Step 1：解除綁定（最優先）
                    if (msg.Contains("解除綁定"))
                    {
                        _conversationStore[userId] = new BookingStateDto
                        {
                            Step = 999 // 確認解除綁定狀態
                        };

                        _bot.ReplyMessage(replyToken,
                            "⚠️ 您確定要解除帳號綁定嗎？\n\n" +
                            "請回覆：「確認解除」以繼續，或「取消」放棄操作。");
                        continue;
                    }

                    // 🔹 Step 2：確認解除綁定
                    if (_conversationStore.ContainsKey(userId))
                    {
                        var state = _conversationStore[userId];
                        if (state.Step == 999)
                        {
                            if (msg.Contains("確認解除"))
                            {
                                var user = await _db.Users.FirstOrDefaultAsync(u => u.LineUserId == userId);
                                if (user != null)
                                {
                                    user.LineUserId = null;
                                    await _db.SaveChangesAsync();

                                    _bot.ReplyMessage(replyToken,
                                        "✅ 您的帳號已解除綁定。\n若要重新綁定，請輸入「綁定帳號」。");
                                }
                                else
                                {
                                    _bot.ReplyMessage(replyToken, "⚠️ 找不到綁定資料，可能已經解除過。");
                                }

                                _conversationStore.Remove(userId);
                                continue;
                            }

                            if (msg.Contains("取消"))
                            {
                                _bot.ReplyMessage(replyToken, "❎ 已取消解除綁定操作。");
                                _conversationStore.Remove(userId);
                                continue;
                            }
                        }
                    }

                    // 🔹 Step 3：綁定帳號
                    if (msg.Contains("綁定帳號"))
                    {
                        _conversationStore[userId] = new BookingStateDto
                        {
                            Step = 900, // 等待帳號
                            BindAccount = null
                        };
                        _bot.ReplyMessage(replyToken, "請輸入您的帳號：");
                        continue;
                    }

                    // 🔹 Step 4：綁定流程
                    if (_conversationStore.ContainsKey(userId))
                    {
                        var state = _conversationStore[userId];

                        // Step 900：等待帳號
                        if (state.Step == 900)
                        {
                            state.BindAccount = msg.Trim();
                            state.Step = 901; // 等待密碼
                            _bot.ReplyMessage(replyToken, "請輸入您的密碼：");
                            continue;
                        }

                        // Step 901：驗證帳密
                        if (state.Step == 901)
                        {
                            var account = state.BindAccount;
                            var password = msg.Trim();

                            var user = await _db.Users.FirstOrDefaultAsync(u => u.Account == account);
                            if (user != null && BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                            {
                                user.LineUserId = userId;
                                await _db.SaveChangesAsync();

                                _bot.ReplyMessage(replyToken, $"✅ 帳號綁定成功！{user.DisplayName} 您好～");

                                _conversationStore[userId] = new BookingStateDto { Step = 1 };
                                SafeReply(replyToken, ApplicantTemplate.BuildStep1());
                            }
                            else
                            {
                                _bot.ReplyMessage(replyToken, "❌ 帳號或密碼錯誤，請輸入「綁定帳號」重新開始。");
                                _conversationStore.Remove(userId);
                            }
                            continue;
                        }
                    }

                    // 🔹 Step 5：預約車輛流程開始
                    if (msg.Contains("預約車輛"))
                    {
                        _conversationStore[userId] = new BookingStateDto { Step = 1 };
                        SafeReply(replyToken, ApplicantTemplate.BuildStep1());
                        continue;
                    }

                    // 🔹 Step 6：取消派車
                    if (msg.Contains("取消"))
                    {
                        _conversationStore.Remove(userId);
                        _bot.ReplyMessage(replyToken, "已取消派車申請流程。");
                        continue;
                    }

                    // 🔹 Step 7：預約流程邏輯
                    if (_conversationStore.ContainsKey(userId))
                    {
                        var state = _conversationStore[userId];

                        switch (state.Step)
                        {
                            // Step 1：選擇即時或預訂時間
                            case 1:
                                state.ReserveType = msg.Contains("即時") ? "now" : "reserve";

                                if (state.ReserveType == "now")
                                {
                                    state.Step = 2;
                                    SafeReply(replyToken, ApplicantTemplate.BuildArrivalTimeOptions(DateTime.Now));
                                }
                                else
                                {
                                    state.Step = 2;
                                    SafeReply(replyToken, ApplicantTemplate.BuildDepartureTimeOptions(DateTime.Now));
                                    _conversationStore[userId] = state;
                                }
                                break;



                            // Step 2：選擇或輸入時間

                            case 2:
                                Console.WriteLine($"[DEBUG] 進入 Step 2, ReserveType={state.ReserveType}, Step={state.Step}, Departure={state.DepartureTime}, Arrival={state.ArrivalTime}");

                                if (msg.Contains("手動輸入出發時間") || msg.Contains("手動輸入抵達時間"))
                                {
                                    _bot.ReplyMessage(replyToken, "請直接輸入時間，例如 09:00");
                                    break;
                                }

                                var rawText = msg
                                    .Replace("出發時間", "")
                                    .Replace("抵達時間", "")
                                    .Replace("：", "") // 全形冒號
                                    .Trim();

                                Console.WriteLine($"[DEBUG] Raw 時間字串：'{rawText}'");

                                DateTime chosenTime;
                                bool parsed = DateTime.TryParseExact(
                                    rawText,
                                    new[] { "yyyy/MM/dd HH:mm", "yyyy/M/d HH:mm", "yyyy-MM-dd HH:mm" },
                                    CultureInfo.InvariantCulture,
                                    DateTimeStyles.AllowWhiteSpaces,
                                    out chosenTime
                                );

                                Console.WriteLine($"[DEBUG] TryParseExact 成功？{parsed} 解析結果：{chosenTime}");

                                if (parsed)
                                {
                                    Console.WriteLine($"[DEBUG] 更新狀態 => Step={state.Step}, Departure={state.DepartureTime}, Arrival={state.ArrivalTime}");

                                    // 自訂時間
                                    if (state.ReserveType == "reserve" && state.DepartureTime == null)
                                    {
                                        // 第一次 → 出發時間
                                        state.DepartureTime = chosenTime;
                                        state.Step = 25; // 等待抵達時間
                                        SafeReply(replyToken, ApplicantTemplate.BuildArrivalTimeOptions(chosenTime));
                                        break;
                                    }

                                    if (state.ReserveType == "reserve" && state.Step == 25)
                                    {
                                        // 第二次 → 抵達時間
                                        state.ArrivalTime = chosenTime;
                                        state.Step = 3; // 下一步
                                        SafeReply(replyToken, ApplicantTemplate.BuildStep3());
                                        break;
                                    }


                                    // 即時預約
                                    if (state.ReserveType == "now")
                                    {
                                        Console.WriteLine($"[DEBUG] 更新狀態 => Step={state.Step}, Departure={state.DepartureTime}, Arrival={state.ArrivalTime}");

                                        state.DepartureTime = chosenTime;
                                        state.Step = 3;
                                        SafeReply(replyToken, ApplicantTemplate.BuildStep3());
                                        break;
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"[ERROR] 無法解析時間：'{rawText}'");
                                    _bot.ReplyMessage(replyToken, "請選擇或輸入正確的時間，例如 09:00");
                                }
                                break;
                            // Step 25：等待使用者選擇抵達時間
                            case 25:
                                if (DateTime.TryParse(msg.Replace("抵達時間", "").Trim(), out var arrivalTime))
                                {
                                    state.ArrivalTime = arrivalTime;
                                    state.Step = 3;
                                    SafeReply(replyToken, ApplicantTemplate.BuildStep3());
                                }
                                else
                                {
                                    _bot.ReplyMessage(replyToken, "請選擇或輸入正確的抵達時間，例如 09:00");
                                }
                                break;


                            // Step 3：用車事由
                            case 3:
                                state.Reason = msg;
                                state.Step = 4;
                                SafeReply(replyToken, ApplicantTemplate.BuildStep4());
                                break;
                            // Step 4：乘客人數
                            case 4:
                                state.PassengerCount = int.TryParse(msg.Replace("人", ""), out var n) ? n : 1;
                                state.Step = 5;
                                SafeReply(replyToken, ApplicantTemplate.BuildStep5_Origin());
                                break;
                            // Step 5：出發地
                            case 5:
                                state.Origin = msg;
                                state.Step = 6;
                                SafeReply(replyToken, ApplicantTemplate.BuildStep5_Destination());
                                break;
                            // Step 6：目的地
                            case 6:
                                state.Destination = msg;
                                state.Step = 7;
                                SafeReply(replyToken, ApplicantTemplate.BuildStep6());
                                break;
                            // Step 7：物品名稱
                            case 7:
                                state.MaterialName = msg;
                                state.Step = 8;
                                SafeReply(replyToken, ApplicantTemplate.BuildStep7());
                                break;
                            // Step 8：單程 or 來回
                            case 8:
                                state.TripType = msg.Contains("來回") ? "round" : "single";
                                state.Step = 9;
                                SafeReply(replyToken, ApplicantTemplate.BuildStep8(state));
                                break;
                            // Step 9：確認送出申請
                            case 9:
                                if (msg.Contains("確認"))
                                {

                                    var (ok, msgText, app) = await _carApplicationService.CreateForLineAsync(state.ToCarAppDto(), userId);

                                    if (!ok)
                                    {
                                        _bot.ReplyMessage(replyToken, $"❌ 送出失敗：{msgText}");
                                        _conversationStore.Remove(userId);
                                        continue;
                                    }

                                    _bot.ReplyMessage(replyToken, "✅ 已送出派車申請，等待管理員審核。");

                                    // 用 BookingStateDto 自己轉好的 DTO 來建 Flex
                                    var reviewJson = ManagerTemplate.BuildManagerReviewBubble(state.ToCarAppDto());

                                    var admins = await _db.Users
                                        .Where(u => u.Role == "Admin" && !string.IsNullOrEmpty(u.LineUserId))
                                        .Select(u => u.LineUserId)
                                        .ToListAsync();

                                    foreach (var adminLineId in admins)
                                        SafePush(adminLineId, reviewJson);

                                    _conversationStore.Remove(userId);
                                    continue;
                                }

                                else
                                {
                                    _bot.ReplyMessage(replyToken, "❌ 已取消申請。");
                                    _conversationStore.Remove(userId);
                                }
                                break;
                        }
                        continue;
                    }

                    // 沒有匹配流程
                    _bot.ReplyMessage(replyToken, $"你說了：{msg}");
                }

                // === Postback 事件 ===
                else if (e.type == "postback")
                {
                    var data = e.postback.data ?? "";

                    if (data.StartsWith("action=setPassengerCount"))
                    {
                        if (_conversationStore.ContainsKey(userId))
                        {
                            var state = _conversationStore[userId];
                            var value = data.Split("value=").LastOrDefault();
                            if (int.TryParse(value, out var count))
                            {
                                state.PassengerCount = count;
                                state.Step = 5;
                                SafeReply(replyToken, ApplicantTemplate.BuildStep5_Origin());
                            }
                        }
                        continue;
                    }

                    if (data.StartsWith("action=setTripType"))
                    {
                        if (_conversationStore.ContainsKey(userId))
                        {
                            var state = _conversationStore[userId];
                            var value = data.Split("value=").LastOrDefault();
                            state.TripType = value == "roundtrip" ? "round" : "single";
                            state.Step = 9;
                            SafeReply(replyToken, ApplicantTemplate.BuildStep8(state));
                        }
                        continue;
                    }

                    //  確認送出申請
                    if (data.StartsWith("action=confirmApplication"))
                    {
                        if (_conversationStore.ContainsKey(userId))
                        {
                            var state = _conversationStore[userId];

                            var (ok, msgText, app) = await _carApplicationService.CreateForLineAsync(state.ToCarAppDto(), userId);
                            if (ok)
                            {
                                _bot.ReplyMessage(replyToken, "✅ 已送出派車申請，等待管理員審核。");
                            }
                            else
                            {
                                _bot.ReplyMessage(replyToken, $"⚠️ 送出失敗：{msgText}");
                            }

                            _conversationStore.Remove(userId);
                        }
                        else
                        {
                            _bot.ReplyMessage(replyToken, "⚠️ 無法確認申請，請重新開始預約流程。");
                        }
                        continue;
                    }
                }

            }

            return Ok();
        }
    }
}
