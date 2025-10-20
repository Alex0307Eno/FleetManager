using Cars.Data;
using Cars.Models;
using Cars.Application.Services.Line;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace Cars.Application.Services
{
    public class DispatchService
    {
        private readonly ApplicationDbContext _db;
        private readonly NotificationService _notificationService;


        public DispatchService(ApplicationDbContext db, NotificationService notificationService)
        {
            _db = db;
            _notificationService = notificationService;
        }

        public async Task<(bool Success, string Message)> UpdateDispatchAsync(
            int dispatchId,
            int? driverId,
            int? vehicleId,
            string byUser,
            string byUserId = null)
        {
            var dispatch = await _db.Dispatches.FindAsync(dispatchId);

            // 舊值記錄
            var oldDriverId = dispatch.DriverId;
            var oldVehicleId = dispatch.VehicleId;

            // 更新資料
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

            // 取得駕駛與車輛資訊
            var oldDriver = oldDriverId.HasValue
                ? await _db.Drivers.AsNoTracking().FirstOrDefaultAsync(d => d.DriverId == oldDriverId)
                : null;
            var oldVehicle = oldVehicleId.HasValue
                ? await _db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.VehicleId == oldVehicleId)
                : null;
            var newDriver = driverId.HasValue
                ? await _db.Drivers.AsNoTracking().FirstOrDefaultAsync(d => d.DriverId == driverId)
                : null;
            var newVehicle = vehicleId.HasValue
                ? await _db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.VehicleId == vehicleId)
                : null;

            // 寫入異動紀錄（使用名稱與車牌）
            _db.DispatchAudits.Add(new DispatchAudit
            {
                DispatchId = dispatch.DispatchId,
                Action = "指派更新",
                OldValue = JsonSerializer.Serialize(new
                {
                    舊駕駛 = oldDriver?.DriverName ?? "(無)",
                    舊車輛 = oldVehicle?.PlateNo ?? "(無)"
                }),
                NewValue = JsonSerializer.Serialize(new
                {
                    新駕駛 = newDriver?.DriverName ?? "(無)",
                    新車輛 = newVehicle?.PlateNo ?? "(無)"
                }),
                ByUserId = byUserId,
                ByUserName = byUser,
                At = DateTime.UtcNow
            });


            await _db.SaveChangesAsync();

            // 通知
            await _notificationService.SendDispatchUpdateAsync(dispatchId);

            return (true, "更新完成");
        }
    }
}
