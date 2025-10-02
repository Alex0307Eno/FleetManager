using Cars.Data;
using Microsoft.EntityFrameworkCore;

namespace Cars.Services
{
    public class LineBotNotificationService
    {
        private readonly ApplicationDbContext _db;

        public LineBotNotificationService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task SendRideReminderAsync(int dispatchId, string type)
        {
            var d = await _db.Dispatches
                .Include(x => x.CarApplication).ThenInclude(a => a.Applicant)
                .Include(x => x.Driver)
                .Include(x => x.Vehicle)
                .FirstOrDefaultAsync(x => x.DispatchId == dispatchId);

            if (d == null) return;

            var app = d.CarApplication;
            var plate = d.Vehicle?.PlateNo ?? "未指派";
            var driverName = d.Driver?.DriverName ?? "未指派";

            var text =
$@"⏰ 乘車提醒（{(type == "D1" ? "前一日" : "15 分鐘前")}）
📅 {app.UseStart:yyyy/MM/dd HH:mm} → {app.UseEnd:HH:mm}
🚗 車號：{plate}
🧑 駕駛：{driverName}
📍 {app.Origin} → {app.Destination}";

            // 這裡先用 Console 模擬，之後可換成推 LINE
            Console.WriteLine(text);
        }
    }
}
