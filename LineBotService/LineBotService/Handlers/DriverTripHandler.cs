using Cars.Application.Services;
using Cars.Data;
using isRock.LineBot;
using Microsoft.EntityFrameworkCore;

namespace LineBotService.Handlers
{
    public class DriverTripHandler
    {
        private readonly Bot _bot;
        private readonly ApplicationDbContext _db;
        private readonly Odometer _dispatchService;

        public DriverTripHandler(Bot bot, ApplicationDbContext db, Odometer dispatchService)
        {
            _bot = bot;
            _db = db;
            _dispatchService = dispatchService;
        }

        public async Task<bool> TryHandleAsync(string msg, string replyToken, string userId)
        {
            // ======= 處理里程輸入 =======
            if (Odometer.DriverInputState.Waiting.TryGetValue(userId, out var mode))
            {
                int odometer;
                if (!int.TryParse(msg, out odometer))
                {
                    _bot.ReplyMessage(replyToken, "⚠️ 請輸入正確的里程數（整數公里）");
                    return true;
                }

                var user = await _db.Users.FirstOrDefaultAsync(u => u.LineUserId == userId);
                if (user == null)
                {
                    _bot.ReplyMessage(replyToken, "⚠️ 找不到綁定的使用者帳號。");
                    return true;
                }

                // 驗角色，只有司機能登記里程
                if (user.Role != "Driver")
                {
                    _bot.ReplyMessage(replyToken, "🚫 只有司機可以輸入行程里程。");
                    Odometer.DriverInputState.Waiting.TryRemove(userId, out _);
                    return true;
                }

                var driver = await _db.Drivers.FirstOrDefaultAsync(d => d.UserId == user.UserId);
                if (driver == null)
                {
                    _bot.ReplyMessage(replyToken, "⚠️ 找不到你的司機資料。");
                    return true;
                }

                var dispatch = await _db.Dispatches
                    .Where(d => d.DriverId == driver.DriverId && d.DispatchStatus != "已完成")
                    .OrderByDescending(d => d.DispatchId)
                    .FirstOrDefaultAsync();

                if (dispatch == null)
                {
                    _bot.ReplyMessage(replyToken, "⚠️ 找不到進行中的派車單。");
                    return true;
                }

                string result;
                if (mode == "start")
                    result = await _dispatchService.SaveStartOdometerAsync(dispatch.DispatchId, driver.DriverId, odometer);
                else
                    result = await _dispatchService.SaveEndOdometerAsync(dispatch.DispatchId, driver.DriverId, odometer);

                Odometer.DriverInputState.Waiting.TryRemove(userId, out _);
                _bot.ReplyMessage(replyToken, result);
                return true;
            }

            // ======= 開始行程 =======
            if (msg == "開始行程")
            {
                var user = await _db.Users.FirstOrDefaultAsync(u => u.LineUserId == userId);
                if (user == null)
                {
                    _bot.ReplyMessage(replyToken, "⚠️ 你尚未綁定帳號。");
                    return true;
                }

                if (user.Role != "Driver")
                {
                    _bot.ReplyMessage(replyToken, "🚫 只有司機可以開始行程。");
                    return true;
                }

                Odometer.DriverInputState.Waiting[userId] = "start";
                _bot.ReplyMessage(replyToken, "請輸入出發里程數（公里）");
                return true;
            }

            // ======= 結束行程 =======
            if (msg == "結束行程")
            {
                var user = await _db.Users.FirstOrDefaultAsync(u => u.LineUserId == userId);
                if (user == null)
                {
                    _bot.ReplyMessage(replyToken, "⚠️ 你尚未綁定帳號。");
                    return true;
                }

                if (user.Role != "Driver")
                {
                    _bot.ReplyMessage(replyToken, "🚫 只有司機可以結束行程。");
                    return true;
                }

                Odometer.DriverInputState.Waiting[userId] = "end";
                _bot.ReplyMessage(replyToken, "請輸入回程里程數（公里）");
                return true;
            }

            return false;
        }
    }
}
