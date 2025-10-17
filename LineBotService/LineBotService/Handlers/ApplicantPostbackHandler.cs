using Cars.Application.Services;
using Cars.Data;
using Cars.Shared.Line;
using isRock.LineBot;
using Microsoft.EntityFrameworkCore;
using LineBotService.Helpers;

namespace LineBotService.Handlers
{
    public class ApplicantPostbackHandler
    {
        private readonly Bot _bot;
        private readonly ApplicationDbContext _db;
        private readonly CarApplicationService _carService;

        public ApplicantPostbackHandler(Bot bot, ApplicationDbContext db, CarApplicationService carService)
        {
            _bot = bot;
            _db = db;
            _carService = carService;
        }

        public async Task HandleAsync(dynamic e, string replyToken, string userId, string data)
        {
            // === 乘客數設定 ===
            if (data.StartsWith("action=setPassengerCount"))
            {
                var val = data.Split("value=").LastOrDefault();
                if (LineBotUtils.Conversations.ContainsKey(userId))
                {
                    var state = LineBotUtils.Conversations[userId];
                    if (int.TryParse(val, out var count))
                    {
                        state.PassengerCount = count;
                        state.Step = 5;
                        LineBotUtils.SafeReply(_bot, replyToken, ApplicantTemplate.BuildStep5_Origin());
                    }
                }
                return;
            }

            // === 單程 / 來回 ===
            if (data.StartsWith("action=setTripType"))
            {
                var val = data.Split("value=").LastOrDefault();
                if (LineBotUtils.Conversations.ContainsKey(userId))
                {
                    var state = LineBotUtils.Conversations[userId];
                    state.TripType = val == "round" ? "round" : "single";
                    state.Step = 9;
                    LineBotUtils.SafeReply(_bot, replyToken, ApplicantTemplate.BuildStep8(state));
                }
                return;
            }

            // === 送出申請 ===
            if (data.StartsWith("action=confirmApplication"))
            {
                if (!LineBotUtils.Conversations.ContainsKey(userId))
                {
                    _bot.ReplyMessage(replyToken, "⚠️ 無法確認申請，請重新開始。");
                    return;
                }

                var state = LineBotUtils.Conversations[userId];
                var dto = state.ToCarAppDto();

                var (ok, msgText, app) = await _carService.CreateForLineAsync(dto, userId);
                if (!ok)
                {
                    _bot.ReplyMessage(replyToken, $"⚠️ 送出失敗：{msgText}");
                    return;
                }

                _bot.ReplyMessage(replyToken, "✅ 已送出派車申請，等待管理員審核。");

                var reviewJson = ManagerTemplate.BuildManagerReviewBubble(dto);

                var admins = await _db.Users
                    .Where(u => (u.Role == "Admin" || u.Role == "Manager") && u.LineUserId != null)
                    .Select(u => u.LineUserId)
                    .ToListAsync();

                foreach (var adminId in admins)
                    LineBotUtils.SafePush(_bot, adminId, reviewJson);

                LineBotUtils.Conversations.Remove(userId);
            }
            // === 管理員審核清單分頁 ===
            if (data.StartsWith("action=reviewListPage"))
            {
                var pageStr = data.Split("page=").LastOrDefault();
                int.TryParse(pageStr, out var page);
                if (page <= 0) page = 1;

                const int pageSize = 5;
                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);

                var apps = await _db.CarApplications
                    .Include(a => a.Applicant)
                    .Where(a => a.Status == "待審核" && a.UseStart >= today && a.UseEnd < tomorrow)
                    .OrderBy(a => a.UseStart)
                    .Select(a => new Cars.Shared.Dtos.CarApplications.CarApplicationDto
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

                // 沒資料 → 回「文字訊息」的 JSON 字串
                if (apps == null || !apps.Any())
                {
                    var textJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        type = "text",
                        text = "目前沒有待審核申請單"
                    });
                    LineBotUtils.SafeReply(_bot, replyToken, textJson);
                    return;
                }

                // 只產 bubble（注意：BuildPendingListBubble 要回 bubble JSON，而不是整個 flex）
                var bubbleJson = ManagerTemplate.BuildPendingListBubble(apps, page, pageSize);

                // 包 flex 外層，然後整包序列化成字串再丟進 SafeReply
                var bubbleObj = System.Text.Json.JsonSerializer.Deserialize<object>(bubbleJson);
                var flexWrapper = new
                {
                    type = "flex",
                    altText = "待審核派車清單",
                    contents = bubbleObj
                };
                var flexJson = System.Text.Json.JsonSerializer.Serialize(flexWrapper);

                LineBotUtils.SafeReply(_bot, replyToken, flexJson);
                return;
            }



        }
    }
}
