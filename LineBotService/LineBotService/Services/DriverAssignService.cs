
using Cars.Data;
using Cars.Shared.Line;
using isRock.LineBot;
using LineBotService.Helpers;
using Microsoft.EntityFrameworkCore;

namespace LineBotService.Core.Services
{
    public class DriverAssignService
    {
        private readonly ApplicationDbContext _db;
        private readonly Bot _bot;

        public DriverAssignService(ApplicationDbContext db, Bot bot)
        {
            _db = db;
            _bot = bot;
        }

        public async Task<(bool Success, string Message)> AssignDriverAsync(int applyId, int driverId)
        {
            // === 1️ 取得資料 ===
            var app = await _db.CarApplications
                .Include(a => a.Vehicle)
                .Include(a => a.Applicant)
                .FirstOrDefaultAsync(a => a.ApplyId == applyId);
            if (app == null) return (false, "找不到派車申請單。");
            var applicantName = app.Applicant?.Name ?? "未填";
            var dispatch = await _db.Dispatches.FirstOrDefaultAsync(d => d.ApplyId == applyId);
            if (dispatch == null) return (false, "找不到派工資料。");

            var driver = await _db.Drivers.FirstOrDefaultAsync(d => d.DriverId == driverId);
            if (driver == null) return (false, "找不到駕駛資料。");

            // === 2️ 透過 Driver → 找 User 取 LINE ID ===
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == driver.UserId);
            var driverLineId = user?.LineUserId;

            // === 3️ 更新狀態 ===
            dispatch.DriverId = driver.DriverId;
            dispatch.DispatchStatus = "已派駕駛";
            app.DriverId = driver.DriverId;
            await _db.SaveChangesAsync();

            // === 4️ 建立通知模板 ===
            var driverBubble = ManagerTemplate.BuildDriverAssignedBubble(
                driver.DriverName,
                app.Vehicle?.PlateNo ?? "未指定",
                app.Origin ?? "未知地點",
                app.Destination ?? "未知地點",
                app.UseStart
            );

            var adminBubble = ManagerTemplate.BuildManagerDispatchDoneBubble(
                applicantName,
                driver.DriverName,
                app.Vehicle?.PlateNo ?? "未指定",
                app.Origin ?? "未知地點",
                app.Destination ?? "未知地點",
                app.UseStart
            );

            // === 5️ 通知駕駛 ===
            if (!string.IsNullOrEmpty(driverLineId))
            {
                LineBotUtils.SafePush(_bot, driverLineId, driverBubble);
                Console.WriteLine($"✅ 已通知駕駛 {driver.DriverName} ({driverLineId})");
            }
            else
            {
                Console.WriteLine($"⚠️ 駕駛 {driver.DriverName} 尚未綁定 LINE 帳號");
            }

            // === 6️ 通知管理員 ===
            var adminIds = await _db.Users
                .Where(u => (u.Role == "Admin" || u.Role == "Manager") && u.LineUserId != null)
                .Select(u => u.LineUserId)
                .ToListAsync();

            foreach (var adminId in adminIds)

                LineBotUtils.SafePush(_bot, adminId, adminBubble);
            Console.WriteLine($"📣 已通知 {adminIds.Count} 位管理員。");

            return (true, $"駕駛「{driver.DriverName}」已成功指派並通知相關人員。");
        }
    }
}
