using Cars.Data;
using Cars.Models;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Cars.Services
{
    public class DispatchService
    {
        private readonly ApplicationDbContext _db;
        private readonly string _driverStartMenuId;
        private readonly string _driverEndMenuId;


        public DispatchService(
            ApplicationDbContext db,
            IOptions<RichMenuOptions> menuOptions)
        {
            _db = db;

            // 從 appsettings.json 讀設定
            _driverStartMenuId = menuOptions.Value.DriverStart;
            _driverEndMenuId = menuOptions.Value.DriverEnd;
        }


       
        // 建一個靜態的暫存 
        public static class DriverInputState
        {
            public static ConcurrentDictionary<string, string> Waiting = new ConcurrentDictionary<string, string>();
        }
        //儲存開始里程
        public async Task<string> SaveStartOdometerAsync(int dispatchId, int driverId, int odometer)
        {
            if (odometer <= 0) return "⚠️ 里程數必須大於 0。";

            var dispatch = await _db.Dispatches
                .Include(d => d.Vehicle)
                .Include(d => d.CarApplication)
                .FirstOrDefaultAsync(d => d.DispatchId == dispatchId);

            if (dispatch == null) return "⚠️ 找不到派車單。";
            if (dispatch.DriverId != driverId) return "⚠️ 此派車單不是你的，無法開始。";
            if (dispatch.StartTime.HasValue) return "⚠️ 此派車單已開始。";
            if (!string.Equals(dispatch.DispatchStatus, "已派車")) return "⚠️ 尚未派車，不能開始。";

            // 防倒退
            var vehicle = dispatch.Vehicle;
            if (vehicle?.Odometer is int cur && odometer < cur)
                return $"⚠️ 起始里程不可小於目前車輛里程（{cur}）。";

            dispatch.OdometerStart = odometer;
            dispatch.StartTime = DateTime.Now;
            dispatch.DispatchStatus = "行程中";

            await _db.SaveChangesAsync();
            return $"✅ 已記錄出發里程：{odometer} km\n行程已開始。";
        }


        public async Task<string> SaveEndOdometerAsync(int dispatchId, int driverId, int odometer)
        {
            if (odometer <= 0) return "⚠️ 里程數必須大於 0。";

            var dispatch = await _db.Dispatches
                .Include(d => d.Vehicle)
                .FirstOrDefaultAsync(d => d.DispatchId == dispatchId);

            if (dispatch == null) return "⚠️ 找不到派車單。";
            if (dispatch.DriverId != driverId) return "⚠️ 此派車單不是你的，無法結束。";
            if (!dispatch.StartTime.HasValue) return "⚠️ 尚未開始行程，無法結束。";
            if (dispatch.EndTime.HasValue) return "⚠️ 已結束行程，不能重複結束。";
            if (dispatch.OdometerStart.HasValue && odometer < dispatch.OdometerStart.Value)
                return $"⚠️ 結束里程 ({odometer}) 不可以小於出發里程 ({dispatch.OdometerStart.Value})。";

            var vehicle = dispatch.Vehicle;
            if (vehicle?.Odometer is int cur && odometer < cur)
                return $"⚠️ 結束里程不可小於目前車輛里程（{cur}）。";

            dispatch.OdometerEnd = odometer;
            dispatch.EndTime = DateTime.Now;
            dispatch.DispatchStatus = "已完成";

            if (vehicle != null) vehicle.Odometer = odometer; // 覆蓋目前里程

            await _db.SaveChangesAsync();

            var totalKm = odometer - (dispatch.OdometerStart ?? odometer);
            return $"✅ 已記錄回程里程：{odometer} km\n📏 本次行駛里程：約 {totalKm} km\n行程已完成。";
        }




    }


}
