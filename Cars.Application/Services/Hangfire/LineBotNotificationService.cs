using Cars.Data;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Cars.Services.Hangfire
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

            
            Console.WriteLine(text);
        }

        public async Task SendPendingDispatchReminderAsync()
        {
            var tomorrow = DateTime.Today.AddDays(1);
            var pending = await _db.Dispatches
                .Include(d => d.CarApplication)
                .Where(d =>
                    d.CarApplication.Status == "完成審核" &&
                    d.VehicleId == null &&
                    d.CarApplication.UseStart.Date == tomorrow)
                .ToListAsync();

            if (!pending.Any()) return;

            var sb = new StringBuilder("🚗【派車提醒】\n以下派車單尚未指派車輛：\n");

            foreach (var d in pending)
            {
                sb.AppendLine($"・申請單 {d.CarApplication.ApplyId}：{d.CarApplication.Origin} → {d.CarApplication.Destination}");
            }

            var admins = await _db.Users
                .Where(u => (u.Role == "Admin" || u.Role == "Manager") && u.LineUserId != null)
                .Select(u => u.LineUserId)
                .ToListAsync();

            
        }
    }
}
