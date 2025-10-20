using Cars.Data;
using Cars.Shared.Dtos.CarApplications;
using Cars.Shared.Line;
using isRock.LineBot;
using LineBotService.Helpers;
using Microsoft.EntityFrameworkCore;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace LineBotService.Handlers
{
    public class PendingListHandler
    {
        private readonly Bot _bot;
        private readonly ApplicationDbContext _db;

        public PendingListHandler(Bot bot, ApplicationDbContext db)
        {
            _bot = bot;
            _db = db;
        }

        public async Task<bool> TryHandleAsync(string msg, string replyToken, string userId)
        {
            if (msg != "待審核") return false;

            
            var user = await _db.Users
                .Include(u => u.Applicant)
                .FirstOrDefaultAsync(u => u.LineUserId == userId);

            if (user == null)
            {
                _bot.ReplyMessage(replyToken, "⚠️ 你還沒綁定帳號，請先輸入「綁定帳號」。");
                return true;
            }

            // 權限檢查
            if (user.Role != "Admin" && user.Role != "Manager")
            {
                _bot.ReplyMessage(replyToken, "🚫 只有管理員或主管可以查看待審核清單。");
                return true;
            }

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var query = _db.CarApplications
                .Include(a => a.Applicant)
                .Where(a => a.Status == "待審核" && a.UseStart >= today && a.UseEnd < tomorrow);

            // ✅ Manager 只看自己部門的申請
            if (user.Role == "Manager" && user.Applicant?.Dept != null)
            {
                var managerDept = user.Applicant.Dept;
                query = query.Where(a => a.Applicant != null && a.Applicant.Dept == managerDept);
            }

            var pendingApps = await query
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
                    Status = a.Status,
                    ApplicantDept = a.Applicant != null ? a.Applicant.Dept : null
                })
                .ToListAsync();

            if (!pendingApps.Any())
            {
                var msgText = user.Role == "Manager"
                    ? "📭 你部門目前沒有待審核的申請單。"
                    : "📭 今天沒有待審核的申請單。";

                _bot.ReplyMessage(replyToken, msgText);
                return true;
            }

            var flexJson = ManagerTemplate.BuildPendingListBubble(pendingApps);
            LineBotUtils.SafeReply(_bot, replyToken, flexJson);
            return true;
        }
    }
}
