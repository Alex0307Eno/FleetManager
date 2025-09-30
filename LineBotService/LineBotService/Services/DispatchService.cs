using Cars.Data;
using Cars.Models;
using Cars.Services;
using LineBotDemo.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace LineBotDemo.Services
{
    public class DispatchService
    {
        private readonly ApplicationDbContext _db;
        private readonly RichMenuService _richMenuService;
        private readonly string _driverStartMenuId;
        private readonly string _driverEndMenuId;

        public DispatchService(
            ApplicationDbContext db,
            RichMenuService richMenuService,
            IOptions<RichMenuOptions> menuOptions)
        {
            _db = db;
            _richMenuService = richMenuService;

            // 從 appsettings.json 讀設定
            _driverStartMenuId = menuOptions.Value.DriverStart;
            _driverEndMenuId = menuOptions.Value.DriverEnd;
        }


       
        // 建一個靜態的暫存 (可以放到 DispatchService.cs)
        public static class DriverInputState
        {
            // userId → "StartOdometer:123" 或 "EndOdometer:456"
            public static ConcurrentDictionary<string, string> Waiting = new ConcurrentDictionary<string, string>();
        }
        //儲存開始里程
        public async Task<string> SaveStartOdometerAsync(int driverId, int odometer)
        {
            var dispatch = await _db.Dispatches
                .Where(d => d.DriverId == driverId && d.DispatchStatus == "已派車" && !d.StartTime.HasValue)
                .OrderBy(d => d.DispatchId)
                .FirstOrDefaultAsync();

            if (dispatch == null)
                return "⚠️ 沒有找到尚未開始的派車單。";

            if (odometer <= 0)
                return "⚠️ 里程數必須大於 0。";

            dispatch.OdometerStart = odometer;
            dispatch.StartTime = DateTime.Now;
            //dispatch.DispatchStatus = "執行中";
            await _db.SaveChangesAsync();

            return $"✅ 已記錄出發里程：{odometer} km\n行程已開始。";
        }
        //儲存結束里程
        public async Task<string> SaveEndOdometerAsync(int driverId, int odometer)
        {
            var dispatch = await _db.Dispatches
                .Where(d => d.DriverId == driverId && !d.EndTime.HasValue)
                .OrderByDescending(d => d.DispatchId)
                .FirstOrDefaultAsync();

            if (dispatch == null)
                return "⚠️ 沒有找到正在執行的派車單。";

            if (!dispatch.OdometerStart.HasValue)
                return "⚠️ 找不到出發里程數，請先登錄開始行程。";

            if (odometer < dispatch.OdometerStart.Value)
                return $"⚠️ 結束里程 ({odometer}) 不可以小於出發里程 ({dispatch.OdometerStart.Value})。";

            dispatch.OdometerEnd = odometer;
            dispatch.EndTime = DateTime.Now;
            //dispatch.DispatchStatus = "已完成";
            await _db.SaveChangesAsync();

            var totalKm = odometer - dispatch.OdometerStart.Value;

            return $"✅ 已記錄回程里程：{odometer} km\n" +
                   $"📏 本次行駛里程：約 {totalKm} km\n" +
                   $"行程已完成。";
        }



    }


}
