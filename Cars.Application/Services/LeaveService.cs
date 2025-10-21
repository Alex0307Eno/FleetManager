using Cars.Data;
using Cars.Models;
using LineBotService.Core.Services;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Threading.Tasks;
using  Cars.Application.Services.Line;


namespace Cars.Application.Services
{
    public class LeaveService
    {
        private readonly ApplicationDbContext _db;
        private readonly NotificationService _notification;

        public LeaveService(ApplicationDbContext db, NotificationService notification)
        {
            _db = db;
            _notification = notification;
        }

        // 檢查請假期間受影響的行程
        public async Task<List<Dispatch>> GetAffectedDispatchesAsync(int driverId, DateTime leaveStart, DateTime leaveEnd)
        {
            return await _db.Dispatches
                        .Include(x => x.CarApplication)
                        .Where(x =>
                            x.DriverId == driverId &&
                            x.CarApplication.UseStart <= leaveEnd &&   
                            x.CarApplication.UseEnd >= leaveStart &&
                            x.DispatchStatus == "已派車")
                        .OrderBy(x => x.CarApplication.UseStart)
                        .ToListAsync();

        }

        // 處理駕駛請假 → 找出受影響行程並通知管理員
        public async Task HandleDriverLeaveAsync(int leaveId)
        {
            var leave = await _db.Leaves
                    .Include(x => x.Driver) 
                    .FirstOrDefaultAsync(x => x.LeaveId == leaveId); if (leave == null) return;

            var affected = await GetAffectedDispatchesAsync(leave.DriverId, leave.Start, leave.End);

            // 取得所有管理員的 LineUserId
            var adminIds = await _db.Users
                .Where(u => (u.Role == "Admin" || u.Role == "Manager") && u.LineUserId != null)
                .Select(u => u.LineUserId)
                .ToListAsync();
            var driverName = leave.Driver?.DriverName ?? $"ID:{leave.DriverId}";

            // 沒有受影響的行程
            if (affected.Count == 0)
            {
                foreach (var id in adminIds)
                {
                    await _notification.PushAsync(id, $" 駕駛 {driverName} 請假期間沒有受影響的行程。");
                }
                return;
            }

            // 有受影響的行程 → 組訊息
            var sb = new StringBuilder();
            sb.AppendLine($"駕駛 {driverName} 請假期間有 {affected.Count} 筆行程受影響：");
            foreach (var d in affected)
            {
                sb.AppendLine($"- #{d.DispatchId} {d.StartTime:MM/dd HH:mm}-{d.EndTime:HH:mm} 狀態:{d.DispatchStatus}");
                bool exists = await _db.AffectedDispatches
                .AnyAsync(x => x.DispatchId == d.DispatchId && !x.IsResolved);

                if (!exists)
                {
                    _db.AffectedDispatches.Add(new AffectedDispatch
                    {
                        DispatchId = d.DispatchId,
                        DriverId = leave.DriverId,
                        LeaveId = leave.LeaveId,
                        CreatedAt = DateTime.Now
                    });
                }
            }
            await _db.SaveChangesAsync();


            var text = sb.ToString();

            // 發送給所有管理員
            foreach (var id in adminIds)
            {
                await _notification.PushAsync(id, text);
            }


        }
        public async Task SendTextAsync(string lineUserId, string text)
        {
            if (string.IsNullOrWhiteSpace(lineUserId))
                return;

            try
            {
                await _notification.PushAsync(lineUserId, text);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LineBot SendTextAsync Error] {ex.Message}");
            }
        }

    }
}
