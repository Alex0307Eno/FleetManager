using Cars.Application.Services;
using Cars.Data;
using Cars.Models;
using Cars.Shared.Line;
using isRock.LIFF;
using isRock.LineBot;
using LineBotService.Core.Services;
using LineBotService.Helpers;
using Microsoft.EntityFrameworkCore;
using static System.Net.WebRequestMethods;

namespace LineBotService.Handlers
{
    public class ManagerReviewHandler
    {
        private readonly Bot _bot;
        private readonly ApplicationDbContext _db;
        private readonly AutoDispatcher _autoDispatcher;
        private readonly CarApplicationService _carApplicationService;
        private readonly DispatchService _dispatchService;
        private readonly IHttpContextAccessor _http;

        public ManagerReviewHandler(Bot bot, ApplicationDbContext db, AutoDispatcher autoDispatcher, CarApplicationService carApplicationService, DispatchService dispatchService, IHttpContextAccessor http)
        {
            _bot = bot;
            _db = db;
            _autoDispatcher = autoDispatcher;
            _carApplicationService = carApplicationService;
            _dispatchService = dispatchService;
            _http = http;
        }

        public async Task HandleAsync(dynamic e, string replyToken, string userId, string data)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.LineUserId == userId);
            var byUser = user?.DisplayName ?? "系統";

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

                var status = await _carApplicationService.UpdateStatusAsync(app.ApplyId, "完成審核", byUser);


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

                var oldStatus = app.Status;
                app.Status = "駁回";
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
                var assignService = new DriverAssignService(_db, _bot, _http);
                var (ok, msg) = await assignService.AssignDriverAsync(applyId, driverId);

                // 查出駕駛名稱
                var driver = await _db.Drivers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.DriverId == driverId);

                var driverName = driver?.DriverName ?? "(未知駕駛)";

                //寫入指派紀錄
                var app = await _db.CarApplications.FirstOrDefaultAsync(a => a.ApplyId == applyId);
                if (app == null)
                {
                    _bot.ReplyMessage(replyToken, "⚠️ 找不到該派車申請。");
                    return;
                }
                var status = await _carApplicationService.UpdateStatusAsync(app.ApplyId, "完成審核", byUser);



                _bot.ReplyMessage(replyToken, ok
                    ? $"✅ {msg}"
                    : $"⚠️ 指派失敗：{msg}");
            }
           

        }
    }
}
