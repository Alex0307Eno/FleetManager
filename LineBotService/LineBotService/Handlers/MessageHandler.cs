using Cars.Application.Services;
using Cars.Data;
using Cars.Shared.Dtos.Line;
using Cars.Shared.Line;
using isRock.LineBot;
using LineBotService.Core.Services;
using LineBotService.Helpers;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Cars.Shared.Dtos.CarApplications;

namespace LineBotService.Handlers
{
    public class MessageHandler
    {
        private readonly Bot _bot;
        private readonly ApplicationDbContext _db;
        private readonly DriverService _driverService;
        private readonly VehicleService _vehicleService;
        private readonly CarApplicationService _carApplicationService;
        private readonly IDistanceService _distance;
        public MessageHandler(Bot bot, ApplicationDbContext db, DriverService driverService, VehicleService vehicleService, CarApplicationService carApplicationService, IDistanceService distance)
        {
            _bot = bot;
            _db = db;
            _driverService = driverService;
            _vehicleService = vehicleService;
            _carApplicationService = carApplicationService;
            _distance = distance;
        }
        // 處理文字訊息
        public async Task HandleMessageAsync(dynamic e, string replyToken, string userId)
        {
            var msg = (string)e.message.text ?? "";
            msg = msg.Trim();

            // 綁定流程
            if (await BindFlow.TryHandleAsync(_bot, _db, replyToken, userId, msg))
                return;

            // 預約流程
            if (msg.Contains("預約車輛"))
            {
                LineBotUtils.Conversations[userId] = new BookingStateDto { Step = 1 };
                LineBotUtils.SafeReply(_bot, replyToken, ApplicantTemplate.BuildStep1());
                return;
            }
            if (msg == "待審核")
            {
                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);

                var pendingApps = await _db.CarApplications
                                .Where(a => a.Status == "待審核" && a.UseStart >= today && a.UseEnd < tomorrow)
                                .Include(a => a.Applicant)
                                .OrderBy(a => a.UseStart)
                                .Select(a => new CarApplicationDto
                                {
                                    ApplyId = a.ApplyId,
                                    ApplicantName = a.Applicant != null ? a.Applicant.Name : null,
                                    UseStart = a.UseStart,
                                    UseEnd = a.UseEnd,
                                    Origin = a.Origin,
                                    Destination = a.Destination,
                                    ApplyReason = a.ApplyReason,
                                    Status = a.Status
                                })
                                .ToListAsync();

                if (!pendingApps.Any())
                {
                    _bot.ReplyMessage(replyToken, "📭 今天沒有待審核的申請單。");
                    return;
                }

                var flexJson = ManagerTemplate.BuildPendingListBubble(pendingApps);
                LineBotUtils.SafeReply(_bot, replyToken, flexJson);
                return;
            }
            // 流程中繼續操作
            if (LineBotUtils.Conversations.ContainsKey(userId))
            {
                var state = LineBotUtils.Conversations[userId];
                await HandleBookingStepAsync(state, msg, replyToken, userId);
            }
        }
        // 申請用車流程
        private async Task HandleBookingStepAsync(BookingStateDto state, string msg, string replyToken, string userId)
        {
            switch (state.Step)
            {
                // Step 1：選擇即時或預約
                case 1:
                    state.ReserveType = msg.Contains("即時") ? "now" : "reserve";
                    state.Step = 2;
                    LineBotUtils.SafeReply(_bot, replyToken,ApplicantTemplate.BuildDepartureTimeOptions(DateTime.Now));

                    break;
                // Step 2：選擇或輸入時間
                case 2:
                    {
                        // 先清理
                        msg = InputSanitizer.CleanText(msg ?? "");
                        // 去掉可能的提示字眼
                        var rawText = msg
                            .Replace("出發時間", "", StringComparison.OrdinalIgnoreCase)
                            .Replace("抵達時間", "", StringComparison.OrdinalIgnoreCase)
                            .Replace("：", ":") // 全形冒號轉半形
                            .Trim();

                        Console.WriteLine($"[DEBUG] Step2 RawText: '{rawText}'");

                        DateTime chosenTime;
                        bool parsed = DateTime.TryParseExact(
                            rawText,
                            new[] { "HH:mm", "yyyy/MM/dd HH:mm", "yyyy/M/d HH:mm", "yyyy-MM-dd HH:mm" },
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AllowWhiteSpaces,
                            out chosenTime
                        );

                        Console.WriteLine($"[DEBUG] TryParseExact 成功？{parsed} => {chosenTime}");

                        if (!parsed)
                        {
                            _bot.ReplyMessage(replyToken, "請輸入正確時間格式，例如「09:00」或「2025/10/17 09:00」。");
                            break;
                        }

                        // 判斷流程：reserve 會先填 Departure 再 Arrival；now 直接填 Departure
                        if (state.ReserveType == "reserve" && state.DepartureTime == null)
                        {
                            state.DepartureTime = chosenTime;
                            state.Step = 25; // 等待抵達時間
                            LineBotUtils.SafeReply(_bot, replyToken, ApplicantTemplate.BuildArrivalTimeOptions(chosenTime));
                        }
                        else if (state.ReserveType == "reserve" && state.Step == 25)
                        {
                            state.ArrivalTime = chosenTime;
                            state.Step = 3;
                            LineBotUtils.SafeReply(_bot, replyToken, ApplicantTemplate.BuildStep3());
                        }
                        else if (state.ReserveType == "now")
                        {
                            // 即時預約的出發時間 = 現在
                            state.DepartureTime = DateTime.Now;
                            // 使用者輸入的時間是抵達時間
                            state.ArrivalTime = chosenTime;
                            state.Step = 3;
                            LineBotUtils.SafeReply(_bot, replyToken, ApplicantTemplate.BuildStep3());
                        }
                        else
                        {
                            // 預設情況（保險用）
                            state.DepartureTime = chosenTime;
                            state.Step = 3;
                            LineBotUtils.SafeReply(_bot, replyToken, ApplicantTemplate.BuildStep3());
                        }


                        break;
                    }

                // Step 25：等待使用者選擇抵達時間
                case 25:
                    {
                        msg = InputSanitizer.CleanText(msg ?? "");
                        var raw = msg.Replace("抵達時間", "", StringComparison.OrdinalIgnoreCase)
                                     .Replace("：", ":").Trim();

                        Console.WriteLine($"[DEBUG] Step25 RawText: '{raw}'");

                        if (!DateTime.TryParseExact(raw,
                                new[] { "HH:mm", "yyyy/MM/dd HH:mm", "yyyy/M/d HH:mm", "yyyy-MM-dd HH:mm" },
                                CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var arrivalTime))
                        {
                            _bot.ReplyMessage(replyToken, "請輸入正確抵達時間（例如 11:00 或 2025/10/17 11:00）。");
                            break;
                        }

                        state.ArrivalTime = arrivalTime;
                        state.Step = 3;
                        LineBotUtils.SafeReply(_bot, replyToken, ApplicantTemplate.BuildStep3());
                        break;
                    }

                // Step 3：輸入用車事由
                case 3:
                    msg = InputSanitizer.CleanText(msg);
                    if (!InputValidator.IsValidReason(msg, out var reason, out var err))
                    {
                        _bot.ReplyMessage(replyToken, err);
                        break;
                    }
                    state.Reason = msg;
                    state.Step = 4;
                    LineBotUtils.SafeReply(_bot, replyToken, ApplicantTemplate.BuildStep4());
                    break;
                // Step 4：選擇乘車人數
                case 4:
                    state.PassengerCount = int.TryParse(msg.Replace("人", ""), out var n) ? n : 1;
                    state.Step = 5;
                    LineBotUtils.SafeReply(_bot, replyToken, ApplicantTemplate.BuildStep5_Origin());
                    break;
                // Step 5：輸入出發地
                case 5:
                    msg = InputSanitizer.CleanText(msg);

                    //  第一步：基本格式驗證
                    if (!InputValidator.IsValidLocation(msg))
                    {
                        _bot.ReplyMessage(replyToken, "⚠️ 出發地格式不正確，請重新輸入。");
                        break;
                    }

                    //  第二步：Google Maps 驗證地點存在
                    if (!await _distance.IsValidLocationAsync(msg))
                    {
                        _bot.ReplyMessage(replyToken, $"⚠️ 查無此地點「{msg}」，請重新輸入。");
                        break;
                    }

                    
                    state.Origin = msg;
                    state.Step = 6;
                    LineBotUtils.SafeReply(_bot, replyToken, ApplicantTemplate.BuildStep5_Destination());
                    break;

                // Step 6：輸入目的地
                case 6:
                    msg = InputSanitizer.CleanText(msg);

                    if (!InputValidator.IsValidLocation(msg))
                    {
                        _bot.ReplyMessage(replyToken, "⚠️ 目的地格式不正確，請重新輸入。");
                        break;
                    }

                    if (!await _distance.IsValidLocationAsync(msg))
                    {
                        _bot.ReplyMessage(replyToken, $"⚠️ 查無此地點「{msg}」，請重新輸入。");
                        break;
                    }

                    state.Destination = msg;
                    state.Step = 7;
                    LineBotUtils.SafeReply(_bot, replyToken, ApplicantTemplate.BuildStep6());
                    break;

                // Step 7：輸入攜帶物品
                case 7:
                    msg = InputSanitizer.CleanText(msg);
                    if (!InputValidator.IsValidMaterial(msg, out var material, out var err2))
                    {
                        _bot.ReplyMessage(replyToken, err2);
                        break;
                    }
                    state.MaterialName = msg;
                    state.Step = 8;
                    LineBotUtils.SafeReply(_bot, replyToken, ApplicantTemplate.BuildStep7());
                    break;
                // Step 8：選擇來回或單程
                case 8:
                    state.TripType = msg.Contains("來回") ? "round" : "single";
                    state.Step = 9;
                    LineBotUtils.SafeReply(_bot, replyToken, ApplicantTemplate.BuildStep8(state));
                    break;
            }
        }
    }
}
