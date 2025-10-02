using Cars.Data;
using Cars.Models;
using LineBotService.Services;
using Microsoft.EntityFrameworkCore;


public class NotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly ILinePush _push;

    public NotificationService(ApplicationDbContext db, ILinePush push)
    {
        _db = db;
        _push = push;
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

        await PushToApplicantAndDriverAsync(d, text);
    }

    private async Task PushToApplicantAndDriverAsync(Dispatch d, string text)
    {
        // 申請人 User
        if (d.CarApplication?.Applicant?.UserId != null)
        {
            var applicantUser = await _db.Users
                .FirstOrDefaultAsync(u => u.UserId == d.CarApplication.Applicant.UserId);

            if (!string.IsNullOrEmpty(applicantUser?.LineUserId))
            {
                await _push.PushAsync(applicantUser.LineUserId, text);
            }
        }

        // 駕駛 User
        if (d.Driver?.UserId != null)
        {
            var driverUser = await _db.Users
                .FirstOrDefaultAsync(u => u.UserId == d.Driver.UserId);

            if (!string.IsNullOrEmpty(driverUser?.LineUserId))
            {
                await _push.PushAsync(driverUser.LineUserId, text);
            }
        }
    }

}
