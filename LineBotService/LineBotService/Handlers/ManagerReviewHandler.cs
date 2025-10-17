using Cars.Application.Services;
using Cars.Data;
using Cars.Shared.Line;
using isRock.LineBot;
using LineBotService.Core.Services;
using LineBotService.Helpers;
using Microsoft.EntityFrameworkCore;

namespace LineBotService.Handlers
{
    public class ManagerReviewHandler
    {
        private readonly Bot _bot;
        private readonly ApplicationDbContext _db;
        private readonly AutoDispatcher _autoDispatcher;

        public ManagerReviewHandler(Bot bot, ApplicationDbContext db, AutoDispatcher autoDispatcher)
        {
            _bot = bot;
            _db = db;
            _autoDispatcher = autoDispatcher;
        }

        public async Task HandleAsync(dynamic e, string replyToken, string userId, string data)
        {
            // === 審核通過 ===
            if (data.StartsWith("action=reviewApprove"))
            {
                var applyIdStr = data.Split("applyId=").LastOrDefault();
                if (!int.TryParse(applyIdStr, out var applyId))
                {
                    _bot.ReplyMessage(replyToken, "⚠️ 無法識別申請編號。");
                    return;
                }

                var app = await _db.CarApplications.FirstOrDefaultAsync(a => a.ApplyId == applyId);
                if (app == null)
                {
                    _bot.ReplyMessage(replyToken, "⚠️ 找不到該派車申請。");
                    return;
                }

                app.Status = "完成審核";
                await _db.SaveChangesAsync();

                var dispatch = await _db.Dispatches.FirstOrDefaultAsync(d => d.ApplyId == app.ApplyId);
                if (dispatch == null)
                {
                    _bot.ReplyMessage(replyToken, "⚠️ 找不到對應派工資料。");
                    return;
                }

                var result = await _autoDispatcher.ApproveAndAssignVehicleAsync(dispatch.DispatchId, app.PassengerCount, null);
                if (result.Success)
                {
                    var driverService = new DriverService(_db);
                    var availableDrivers = await driverService.GetAvailableDriversAsync(app.UseStart, app.UseEnd);
                    var flexJson = ManagerTemplate.BuildDriverFlex(app.ApplyId, availableDrivers);

                    _bot.ReplyMessage(replyToken,$"✅ 已完成審核並派車成功！\n🚗 車輛：{result.PlateNo}\n請選擇駕駛："); 
                    LineBotUtils.SafePush(_bot, e.source.userId, flexJson);
                }
                else
                {
                    _bot.ReplyMessage(replyToken, $"⚠️ 已完成審核，但派車失敗：{result.Message}");
                }
                return;
            }

            // === 駁回 ===
            if (data.StartsWith("action=reviewReject"))
            {
                var applyIdStr = data.Split("applyId=").LastOrDefault();
                if (!int.TryParse(applyIdStr, out var applyId))
                {
                    _bot.ReplyMessage(replyToken, "⚠️ 無法識別申請編號。");
                    return;
                }

                var app = await _db.CarApplications.FirstOrDefaultAsync(a => a.ApplyId == applyId);
                if (app == null)
                {
                    _bot.ReplyMessage(replyToken, "⚠️ 找不到該派車申請。");
                    return;
                }

                app.Status = "已駁回";
                await _db.SaveChangesAsync();
                _bot.ReplyMessage(replyToken, $"❌ 已駁回該派車申請（申請編號 {applyId}）。");
                return;
            }

            // === 選駕駛 ===
            if (data.StartsWith("action=selectDriver"))
            {
                // 拆 Query String
                var parts = data.Split('&')
                                .Select(p => p.Split('='))
                                .Where(p => p.Length == 2)
                                .ToDictionary(p => p[0], p => p[1]);

                // 從字典安全取值
                parts.TryGetValue("driverId", out var driverIdStr);
                parts.TryGetValue("applyId", out var applyIdStr);

                if (!int.TryParse(driverIdStr, out var driverId))
                {
                    _bot.ReplyMessage(replyToken, "⚠️ 無法識別駕駛 ID。");
                    return;
                }

                if (!int.TryParse(applyIdStr, out var applyId))
                {
                    _bot.ReplyMessage(replyToken, "⚠️ 無法識別申請單 ID。");
                    return;
                }

                // 呼叫 DriverAssignService
                var assignService = new DriverAssignService(_db, _bot);
                var (ok, msg) = await assignService.AssignDriverAsync(applyId, driverId);

                _bot.ReplyMessage(replyToken, ok
                    ? $"✅ {msg}"
                    : $"⚠️ 指派失敗：{msg}");
            }
            // === 審核清單翻頁 ===
            //if (data.StartsWith("action=reviewListPage"))
            //{
            //    var pageStr = data.Split("page=").LastOrDefault();
            //    int.TryParse(pageStr, out var page);
            //    if (page <= 0) page = 1;

            //    const int pageSize = 5;
            //    var today = DateTime.Today;
            //    var tomorrow = today.AddDays(1);

            //    var apps = await _db.CarApplications
            //        .Include(a => a.Applicant)
            //        .Where(a => a.Status == "待審核" && a.UseStart >= today && a.UseEnd < tomorrow)
            //        .OrderBy(a => a.UseStart)
            //        .Skip((page - 1) * pageSize)
            //        .Take(pageSize)
            //        .Select(a => new Cars.Shared.Dtos.CarApplications.CarApplicationDto
            //        {
            //            ApplyId = a.ApplyId,
            //            ApplicantName = a.Applicant != null ? a.Applicant.Name : null,
            //            UseStart = a.UseStart,
            //            UseEnd = a.UseEnd,
            //            Origin = a.Origin,
            //            Destination = a.Destination,
            //            ApplyReason = a.ApplyReason,
            //            Status = a.Status
            //        })
            //        .ToListAsync();


            //    if (apps == null || !apps.Any())
            //    {
            //        var textJson = System.Text.Json.JsonSerializer.Serialize(new
            //        {
            //            type = "text",
            //            text = "目前沒有待審核申請單"
            //        });
            //        LineBotUtils.SafeReply(_bot, replyToken, textJson);
            //        return;
            //    }

            //    var bubbleJson = ManagerTemplate.BuildPendingListBubble(apps);
            //    var bubbleObj = System.Text.Json.JsonSerializer.Deserialize<object>(bubbleJson);

            //    var flexWrapper = new
            //    {
            //        type = "flex",
            //        altText = "待審核派車清單",
            //        contents = bubbleObj
            //    };
            //    var flexJson = System.Text.Json.JsonSerializer.Serialize(flexWrapper);

            //    LineBotUtils.SafeReply(_bot, replyToken, flexJson);
            //    return;
            //}


        }
    }
}
