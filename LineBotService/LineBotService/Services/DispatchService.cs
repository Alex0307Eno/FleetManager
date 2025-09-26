using Cars.Data;
using Cars.Models;
using LineBotDemo.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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

        /// <summary>
        /// 開始行程 → 設定狀態、時間、切換到結束 RichMenu
        /// </summary>
        public async Task<string> StartTripAsync(int driverId, string userId)
        {
            var now = DateTime.Now;

            // 找一張待開始的任務
            var dispatch = await _db.Dispatches
                .Where(d => d.DriverId == driverId &&
                            d.DispatchStatus == "已派車" &&
                            !d.StartTime.HasValue)
                .OrderBy(d => d.DispatchId)
                .FirstOrDefaultAsync();

            if (dispatch == null)
                return "⚠️ 目前沒有可開始的任務";

            dispatch.DispatchStatus = "執行中";
            dispatch.StartTime = now;
            await _db.SaveChangesAsync();

            // 換成「結束行程」RichMenu
            await _richMenuService.BindToUserAsync(userId, _driverEndMenuId);

            return $"✅ 行程已開始\n任務單號：{dispatch.DispatchId}\n開始時間：{now:HH:mm}";
        }

        /// <summary>
        /// 結束行程 → 設定狀態、時間、切換回開始 RichMenu
        /// </summary>
        public async Task<string> EndTripAsync(int driverId, string userId)
        {
            var now = DateTime.Now;

            var dispatch = await _db.Dispatches
                .Where(d => d.DriverId == driverId && d.DispatchStatus == "執行中")
                .OrderByDescending(d => d.DispatchId)
                .FirstOrDefaultAsync();

            if (dispatch == null)
                return "⚠️ 目前沒有進行中的任務";

            dispatch.DispatchStatus = "已完成";
            dispatch.EndTime = now;
            await _db.SaveChangesAsync();

            // 換回「開始行程」RichMenu
            await _richMenuService.BindToUserAsync(userId, _driverStartMenuId);

            return $"✅ 行程已完成\n任務單號：{dispatch.DispatchId}\n結束時間：{now:HH:mm}";
        }
    }
}
