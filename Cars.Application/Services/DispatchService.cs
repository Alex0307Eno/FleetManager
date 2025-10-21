using Cars.Application.Services.Line;
using Cars.Data;
using Cars.Models;
using Cars.Services.Hangfire;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace Cars.Application.Services
{
    public class DispatchService
    {
        private readonly ApplicationDbContext _db;
        private readonly NotificationService _notificationService;
        private readonly LineBotNotificationService _lineBotNotificationService;

        public DispatchService(ApplicationDbContext db, NotificationService notificationService, LineBotNotificationService lineBotNotificationService)
        {
            _db = db;
            _notificationService = notificationService;
            _lineBotNotificationService = lineBotNotificationService;
        }

        public async Task<(bool Success, string Message)> UpdateDispatchAsync(
    int dispatchId,
    int? driverId,
    int? vehicleId,
    string byUser,
    string byUserId = null)
        {
            var dispatch = await _db.Dispatches
                .Include(d => d.CarApplication)
                .FirstOrDefaultAsync(d => d.DispatchId == dispatchId);

            if (dispatch == null)
                return (false, "派工不存在");

            var oldDriverId = dispatch.DriverId;
            var oldVehicleId = dispatch.VehicleId;

            dispatch.DriverId = driverId;
            dispatch.VehicleId = vehicleId;
            dispatch.DispatchStatus = "已派車";

            var app = await _db.CarApplications.FirstOrDefaultAsync(a => a.ApplyId == dispatch.ApplyId);
            if (app != null)
            {
                app.DriverId = driverId;
                app.VehicleId = vehicleId;
                app.Status = "完成審核";
            }

            await _db.SaveChangesAsync();

            var oldDriver = oldDriverId.HasValue ? await _db.Drivers.AsNoTracking().FirstOrDefaultAsync(d => d.DriverId == oldDriverId) : null;
            var oldVehicle = oldVehicleId.HasValue ? await _db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.VehicleId == oldVehicleId) : null;
            var newDriver = driverId.HasValue ? await _db.Drivers.AsNoTracking().FirstOrDefaultAsync(d => d.DriverId == driverId) : null;
            var newVehicle = vehicleId.HasValue ? await _db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.VehicleId == vehicleId) : null;

            _db.DispatchAudits.Add(new DispatchAudit
            {
                DispatchId = dispatch.DispatchId,
                Action = "指派更新",
                OldValue = JsonSerializer.Serialize(new { 舊駕駛 = oldDriver?.DriverName ?? "(無)", 舊車輛 = oldVehicle?.PlateNo ?? "(無)" }),
                NewValue = JsonSerializer.Serialize(new { 新駕駛 = newDriver?.DriverName ?? "(無)", 新車輛 = newVehicle?.PlateNo ?? "(無)" }),
                ByUserId = byUserId,
                ByUserName = byUser,
                At = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            // ===  新增這段：若該派工原本有受影響紀錄，改為已解決 ===
            var affected = await _db.AffectedDispatches
                .FirstOrDefaultAsync(x => x.DispatchId == dispatchId && !x.IsResolved);

            if (affected != null)
            {
                affected.IsResolved = true;
                affected.ResolvedAt = DateTime.Now;
                await _db.SaveChangesAsync();
                Console.WriteLine($"[INFO] 已標記受影響派工 #{dispatchId} 為已解決。");
            }

            //排程提醒
            DispatchJobScheduler.ScheduleRideReminders(dispatch);

            try
            {
                await _lineBotNotificationService.SendRideReminderAsync(dispatch.DispatchId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LINE ERROR] {ex.Message}");
            }

            await _notificationService.SendDispatchUpdateAsync(dispatchId);

            return (true, "更新完成");
        }

    }
}
