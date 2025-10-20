using Cars.Data;
using Cars.Models;
using LineBotService.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;

namespace Cars.Application.Services.Line;
public class NotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly ILinePush _push;
    private readonly string _token;

    public NotificationService(ApplicationDbContext db, ILinePush push, IConfiguration config)
    {
        _db = db;
        _push = push;
        _token = config["LineBot:ChannelAccessToken"] ?? string.Empty;

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
    // 直接發送 Flex Message
    public async Task PushAsync(string to, string flexJson)
    {
        if (string.IsNullOrWhiteSpace(to) || string.IsNullOrWhiteSpace(flexJson))
            return;

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _token);

        // 這裡把 flexJson 當成已經是完整的 message 物件
        var payload = new
        {
            to,
            messages = new[] { JsonConvert.DeserializeObject(flexJson) }
        };

        var content = new StringContent(
            JsonConvert.SerializeObject(payload),
            Encoding.UTF8,
            "application/json"
        );
        // 送出前，保底確認 messages[0].type 存在
        var msg = JsonConvert.DeserializeObject<JObject>(flexJson);
        if (msg?["type"]?.ToString() != "flex")
        {
            Console.WriteLine("⚠️ flexJson 並非完整 flex message，缺少 type:flex 或模板輸出錯誤。");
        }

        var res = await client.PostAsync("https://api.line.me/v2/bot/message/push", content);
        var resp = await res.Content.ReadAsStringAsync();
        Console.WriteLine($"LINE Push → {res.StatusCode} / {resp}");
    }
    // 派車資訊更新通知
    public async Task SendDispatchUpdateAsync(int dispatchId)
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
        var applicantName = app?.Applicant?.Name ?? "—";

        var text =
        $@"📢 派車資訊更新通知
       🧾 申請人：{applicantName}
       📅 時間：{app?.UseStart:yyyy/MM/dd HH:mm} → {app?.UseEnd:HH:mm}
       🚗 車號：{plate}
       👨‍✈️ 駕駛：{driverName}
       📍 {app?.Origin} → {app?.Destination}
       🔄 請留意異動後的行程安排。";

        await PushToApplicantAndDriverAsync(d, text);
    }

   



}
