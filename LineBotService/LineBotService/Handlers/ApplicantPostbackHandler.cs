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

                // 先取出申請人的部門
                var applicantDept = await _db.Applicants
                    .Where(a => a.ApplicantId == app.ApplicantId)
                    .Select(a => a.Dept)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrEmpty(applicantDept))
                    return; // 沒有部門就不發通知

                // 找出同部門的 Admin / Manager
                var admins = await _db.Users
                    .Include(u => u.Applicant)
                    .Where(u =>
                        (u.Role == "Admin" || u.Role == "Manager") &&
                        u.LineUserId != null &&
                        u.Applicant != null &&
                        u.Applicant.Dept == applicantDept)
                    .Select(u => u.LineUserId)
                    .ToListAsync();


                foreach (var adminId in admins)
                    LineBotUtils.SafePush(_bot, adminId, reviewJson);

                LineBotUtils.Conversations.Remove(userId);
            }
            



        }
    }
}
