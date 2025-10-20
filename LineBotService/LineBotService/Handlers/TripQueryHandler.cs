using Cars.Data;
using isRock.LineBot;
using Microsoft.EntityFrameworkCore;

namespace LineBotService.Handlers
{
    public class TripQueryHandler
    {
        private readonly Bot _bot;
        private readonly ApplicationDbContext _db;

        public TripQueryHandler(Bot bot, ApplicationDbContext db)
        {
            _bot = bot;
            _db = db;
        }

        public async Task<bool> TryHandleTripQueryAsync(string msg, string replyToken, string userId)
        {
            if (!msg.Contains("我的行程"))
                return false;

            // 先確認角色：司機 or 申請人
            var user = await _db.Users.FirstOrDefaultAsync(u => u.LineUserId == userId);
            if (user == null)
            {
                _bot.ReplyMessage(replyToken, "⚠️ 你尚未綁定帳號。");
                return true;
            }

            var driver = await _db.Drivers.FirstOrDefaultAsync(d => d.UserId == user.UserId);
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            if (driver != null)
            {
                // 查司機行程
                var trips = await _db.Dispatches
                    .Include(d => d.CarApplication)
                     .ThenInclude(a => a.Applicant)
                    .Include(d => d.Vehicle)
                    .Where(d => d.DriverId == driver.DriverId &&
                                d.CarApplication.UseStart >= today &&
                                d.CarApplication.UseEnd < tomorrow)
                    .OrderBy(d => d.CarApplication.UseStart)
                    .ToListAsync();

                if (!trips.Any())
                {
                    _bot.ReplyMessage(replyToken, "🚗 你今天沒有派車任務。");
                    return true;
                }

                var list = string.Join("\n\n", trips.Select(t =>
                    $"📍 申請人：{t.CarApplication?.Applicant?.Name}\n" +
                    $"🕒 時間：{t.CarApplication?.UseStart:MM/dd HH:mm} - {t.CarApplication?.UseEnd:HH:mm}\n" +
                    $"🚘 車輛：{t.Vehicle?.PlateNo ?? "未指定"}\n" +
                    $"📍 路線：{t.CarApplication?.Origin} → {t.CarApplication?.Destination}"));

                _bot.ReplyMessage(replyToken, $"以下是你今天的行程：\n\n{list}");
                return true;
            }
            else
            {
                // 查申請人行程
                var apps = await _db.CarApplications
                          .Include(a => a.DispatchOrders)
                              .ThenInclude(d => d.Vehicle)
                          .Include(a => a.DispatchOrders)
                              .ThenInclude(d => d.Driver)
                          .Where(a => a.ApplicantId == user.UserId &&
                                      a.UseStart >= today &&
                                      a.UseEnd < tomorrow)
                          .OrderBy(a => a.UseStart)
                          .ToListAsync();


                if (!apps.Any())
                {
                    _bot.ReplyMessage(replyToken, "📭 你今天沒有任何用車申請。");
                    return true;
                }

                var list = string.Join("\n\n", apps.Select(a =>
                {
                    var first = a.DispatchOrders.FirstOrDefault();
                    var driverName = first != null && first.Driver != null ? first.Driver.DriverName : "尚未派車";
                    var plate = first != null && first.Vehicle != null ? first.Vehicle.PlateNo : "未指定";

                    return $"🕒 時間：{a.UseStart:MM/dd HH:mm} - {a.UseEnd:HH:mm}\n" +
                           $"🚘 司機：{driverName}\n" +
                           $"🚗 車輛：{plate}\n" +
                           $"📍 路線：{a.Origin} → {a.Destination}\n" +
                           $"📄 狀態：{a.Status}";
                }));

                _bot.ReplyMessage(replyToken, $"以下是你今天的行程：\n\n{list}");
                return true;
            }
        }
    }
}
