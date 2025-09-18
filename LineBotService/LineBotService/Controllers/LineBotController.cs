using Cars.Data;
using Cars.Models;
using DocumentFormat.OpenXml.ExtendedProperties;
using DocumentFormat.OpenXml.Spreadsheet;
using isRock.LIFF;
using isRock.LineBot;
using LineBotDemo.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;

namespace LineBotDemo.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LineBotController : ControllerBase
    {
        private readonly string _token;
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _config;
        private readonly string _baseUrl;
        private readonly RichMenuService _richMenuService;

        public LineBotController(IConfiguration config, ApplicationDbContext db,RichMenuService richMenuService)
        {
            _token = config["LineBot:ChannelAccessToken"];
            _baseUrl = config["AppBaseUrl"];
            _db = db;
            _config = config;
            _richMenuService = richMenuService;
        }
        #region 暫存方法
        // ===== 使用者流程暫存（依 userId 分別保存） =====
        private class BookingState
        {
            public string? ReserveTime { get; set; }
            public string? Reason { get; set; }
            public string? PassengerCount { get; set; }
            public string? Origin { get; set; }

            public string? Destination { get; set; }
            public string? TripType { get; set; }   // 單程 or 來回


            // 給管理員指派流程用
            public int? SelectedDriverId { get; set; }
            public string? SelectedDriverName { get; set; }
        }
        // 對話進度暫存
        private static readonly ConcurrentDictionary<string, BookingState> _flow = new();

        // 把「申請單 ApplyId 對應 申請人 LINE userId」暫存起來，方便審核後通知申請人
        private static readonly ConcurrentDictionary<int, string> _applyToApplicant = new();
        #endregion

        #region 對話工具
        // Step 1: 即時預約 or 預訂時間
        private const string Step1JsonArray = @"
[
  {
    ""type"": ""text"",
    ""text"": ""請選擇預約的時間"",
    ""quickReply"": {
      ""items"": [
        { ""type"": ""action"", ""action"": { ""type"": ""message"", ""label"": ""即時預約"", ""text"": ""即時預約"" } },
        { ""type"": ""action"", ""action"": { ""type"": ""message"", ""label"": ""預訂時間"", ""text"": ""預訂時間"" } }
      ]
    }
  }
]";
        // 8:00~17:00，每小時一格 + 手動輸入
        private const string Step2TimeJsonArray = @"
[
  {
    ""type"": ""text"",
    ""text"": ""請選擇出發時間（或點『手動輸入』）"",
    ""quickReply"": {
      ""items"": [
        { ""type"": ""action"", ""action"": { ""type"": ""message"", ""label"": ""08:00"", ""text"": ""08:00"" } },
        { ""type"": ""action"", ""action"": { ""type"": ""message"", ""label"": ""09:00"", ""text"": ""09:00"" } },
        { ""type"": ""action"", ""action"": { ""type"": ""message"", ""label"": ""10:00"", ""text"": ""10:00"" } },
        { ""type"": ""action"", ""action"": { ""type"": ""message"", ""label"": ""11:00"", ""text"": ""11:00"" } },
        { ""type"": ""action"", ""action"": { ""type"": ""message"", ""label"": ""12:00"", ""text"": ""12:00"" } },
        { ""type"": ""action"", ""action"": { ""type"": ""message"", ""label"": ""13:00"", ""text"": ""13:00"" } },
        { ""type"": ""action"", ""action"": { ""type"": ""message"", ""label"": ""14:00"", ""text"": ""14:00"" } },
        { ""type"": ""action"", ""action"": { ""type"": ""message"", ""label"": ""15:00"", ""text"": ""15:00"" } },
        { ""type"": ""action"", ""action"": { ""type"": ""message"", ""label"": ""16:00"", ""text"": ""16:00"" } },
        { ""type"": ""action"", ""action"": { ""type"": ""message"", ""label"": ""17:00"", ""text"": ""17:00"" } },
        { ""type"": ""action"", ""action"": { ""type"": ""message"", ""label"": ""手動輸入"", ""text"": ""手動輸入"" } }
      ]
    }
  }
]";
        // 1~4人
        private const string Step3JsonArray = @"
[
  {
    ""type"": ""text"",
    ""text"": ""請選擇乘客人數"",
    ""quickReply"": {
      ""items"": [
        { ""type"": ""action"", ""action"": { ""type"": ""message"", ""label"": ""1人"", ""text"": ""1人"" } },
        { ""type"": ""action"", ""action"": { ""type"": ""message"", ""label"": ""2人"", ""text"": ""2人"" } },
        { ""type"": ""action"", ""action"": { ""type"": ""message"", ""label"": ""3人"", ""text"": ""3人"" } },
        { ""type"": ""action"", ""action"": { ""type"": ""message"", ""label"": ""4人"", ""text"": ""4人"" } }
      ]
    }
  }
]";
        // 單程 or 來回
        private const string Step6bTripJsonArray = @"
[
  {
    ""type"": ""text"",
    ""text"": ""請選擇行程類型"",
    ""quickReply"": {
      ""items"": [
        { ""type"": ""action"", ""action"": { ""type"": ""message"", ""label"": ""單程"", ""text"": ""單程"" } },
        { ""type"": ""action"", ""action"": { ""type"": ""message"", ""label"": ""來回"", ""text"": ""來回"" } }
      ]
    }
  }
]";
        #endregion

        #region 主流程
        [HttpPost]
        public async Task<IActionResult> Post()
        {

            string body;
            using (var reader = new StreamReader(Request.Body))
                body = await reader.ReadToEndAsync();

            var bot = new Bot(_token);
            var events = isRock.LineBot.Utility.Parsing(body);

            foreach (var ev in events.events)
            {

                var replyToken = ev.replyToken;
                var uid = ev.source.userId ?? "anon";
                //  防呆：檢查使用者是否有綁定
                var dbUser = _db.Users.FirstOrDefault(u => u.LineUserId == uid);
                if (dbUser == null)
                {
                    bot.ReplyMessage(replyToken, "⚠️ 您的 LINE 帳號尚未綁定系統帳號，請聯絡管理員");
                    continue; // 不處理後續流程
                }
                // === 確保 LineUser 與 User 存在 & 同步 DisplayName ===
                try
                {
                    // 特殊事件：加入好友
                    // ================= FOLLOW 事件 =================
                    if (ev.type == "follow")
                    {
                        var userId = ev.source.userId;
                        if (!string.IsNullOrEmpty(userId))
                        {
                            // 查詢使用者角色（先看 Users 表）
                            var role = _db.Users
                                .Where(u => u.LineUserId == userId)
                                .Select(u => u.Role)
                                .FirstOrDefault();

                            // 如果沒有，嘗試從 LineUsers 表查
                            if (string.IsNullOrEmpty(role))
                            {
                                 role = _db.Users
                                        .Where(u => u.LineUserId == userId)
                                        .Select(u => u.Role)
                                        .FirstOrDefault();
                            }

                            // 沒有角色 → 預設 Applicant
                            if (string.IsNullOrEmpty(role))
                            {
                                role = "Applicant";
                            }

                            // 綁定對應角色的 RichMenu
                            await _richMenuService.BindUserToRoleAsync(userId, role);

                            bot.ReplyMessage(replyToken, $"👋 歡迎加入！已為您設定 {role} 選單。");
                        }
                        continue;
                    }

                    var profile = isRock.LineBot.Utility.GetUserInfo(uid, _token);
                    var lineDisplayName = profile.displayName ?? "未命名";


                    // 1. 確保 LineUsers
                    var lineUser = _db.LineUsers.FirstOrDefault(x => x.LineUserId == uid);
                    if (lineUser == null)
                    {
                        lineUser = new LineUser
                        {
                            LineUserId = uid,
                            DisplayName = lineDisplayName,
                            CreatedAt = DateTime.Now
                        };
                        _db.LineUsers.Add(lineUser);
                        _db.SaveChanges();
                    }
                    else
                    {
                        lineUser.DisplayName = lineDisplayName;
                        _db.SaveChanges();
                    }

                    // 2. 確保 Users
                    var user = _db.Users.FirstOrDefault(u => u.LineUserId == uid);
                    if (user == null)
                    {
                        user = new User
                        {
                            Account = uid, 
                            PasswordHash = "",
                            DisplayName = lineDisplayName,
                            Role = "Applicant",
                            IsActive = true,
                            CreatedAt = DateTime.Now,
                            LineUserId = uid
                        };
                        _db.Users.Add(user);
                    }
                    else
                    {
                        user.DisplayName = lineDisplayName; // 同步最新名字
                    }
                    _db.SaveChanges();

                }
                catch (Exception ex)
                {
                    Console.WriteLine("⚠️ 建立 LineUser/User 失敗: " + ex.Message);
                }
                // ================= POSTBACK 事件 =================
                if (ev.type == "postback")
                {
                    var data = ev.postback?.data ?? "";

                    // 解析 postback data
                    var kv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var pair in data.Split('&', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var parts = pair.Split('=', 2);
                        var k = Uri.UnescapeDataString(parts[0]);
                        var v = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
                        kv[k] = v;
                    }

                    // ★ 統一取出 action
                    kv.TryGetValue("action", out var action);

                    // ====== 待審核清單分頁 ======
                    if (action == "reviewListPage")
                    {
                        int.TryParse(kv.GetValueOrDefault("page"), out var page);
                        if (page <= 0) page = 1;

                        var bubble = BuildPendingListBubble(page, 5, _db);
                        if (bubble == null)
                            bot.ReplyMessage(replyToken, "目前沒有待審核的申請。");
                        else
                            bot.ReplyMessageWithJSON(replyToken, $"[{bubble}]");
                        return Ok();
                    }

                    // ====== 同意申請 → 進入選駕駛流程 ======
                    if (action == "reviewApprove")
                    {
                        int.TryParse(kv.GetValueOrDefault("applyId"), out var applyId);
                        var app = _db.CarApplications.FirstOrDefault(a => a.ApplyId == applyId);
                        if (app == null)
                        {
                            bot.ReplyMessage(replyToken, "⚠️ 找不到該申請單。");
                            return Ok();
                        }

                        app.Status = "審核通過(待指派)";
                        _db.SaveChanges();

                        var selectDriverBubble = BuildDriverSelectBubble(applyId, _db);
                        bot.ReplyMessageWithJSON(replyToken, $"[{selectDriverBubble}]");
                        return Ok();
                    }

                    // ====== 拒絕申請 ======
                    if (action == "reviewReject")
                    {
                        int.TryParse(kv.GetValueOrDefault("applyId"), out var applyId);
                        var app = _db.CarApplications.FirstOrDefault(a => a.ApplyId == applyId);
                        if (app == null)
                        {
                            bot.ReplyMessage(replyToken, "⚠️ 找不到該申請單");
                            return Ok();
                        }

                        app.Status = "已拒絕";
                        _db.SaveChanges();

                        if (_applyToApplicant.TryGetValue(applyId, out var applicantUid))
                        {
                            bot.PushMessage(applicantUid,
                                $"❌ 您的派車申請已被拒絕\n事由：{app.ApplyReason}\n地點：{app.Destination}");
                        }

                        bot.ReplyMessage(replyToken, "✅ 已拒絕該申請。");
                        return Ok();
                    }

                    // ========== 指派駕駛 ==========
                    if (action == "assignDriver")
                    {
                        int.TryParse(kv.GetValueOrDefault("applyId"), out var applyId);
                        int.TryParse(kv.GetValueOrDefault("driverId"), out var driverId);
                        var driverName = kv.GetValueOrDefault("driverName");

                        var app = _db.CarApplications.FirstOrDefault(a => a.ApplyId == applyId);
                        if (app == null)
                        {
                            bot.ReplyMessage(replyToken, "⚠️ 找不到該申請單");
                            return Ok();
                        }

                        var state = _flow.GetOrAdd(uid, _ => new BookingState());
                        state.SelectedDriverId = driverId;
                        state.SelectedDriverName = driverName;

                        bot.ReplyMessage(replyToken, $"✅ 已選擇駕駛：{driverName}");

                        var carBubble = BuildCarSelectBubble(applyId, _db);
                        bot.PushMessageWithJSON(uid, $"[{carBubble}]");
                        return Ok();
                    }

                    // ========== 指派車輛 ==========
                    if (action == "assignVehicle")
                    {
                        int.TryParse(kv.GetValueOrDefault("applyId"), out var applyId);
                        int.TryParse(kv.GetValueOrDefault("vehicleId"), out var vehicleId);
                        var plateNo = kv.GetValueOrDefault("plateNo");

                        if (!_flow.TryGetValue(uid, out var driverState))
                        {
                            bot.ReplyMessage(replyToken, "⚠️ 找不到駕駛資訊，請重新操作");
                            return Ok();
                        }

                        var app = _db.CarApplications.FirstOrDefault(a => a.ApplyId == applyId);
                        if (app == null)
                        {
                            bot.ReplyMessage(replyToken, "⚠️ 找不到對應申請單");
                            return Ok();
                        }

                        var dispatch = _db.Dispatches
                            .OrderByDescending(d => d.DispatchId)
                            .FirstOrDefault(d => d.ApplyId == applyId);

                        if (dispatch == null)
                        {
                            bot.ReplyMessage(replyToken, "⚠️ 找不到對應的派車單");
                            return Ok();
                        }

                        dispatch.DriverId = driverState.SelectedDriverId ?? 0;
                        dispatch.VehicleId = vehicleId;
                        dispatch.DispatchStatus = "已派車";
                        dispatch.StartTime = DateTime.Now;
                        dispatch.EndTime = app.UseEnd;

                        double km = 0, minutes = 0;
                        try
                        {
                            using var client = new HttpClient();
                            var url = $"{_baseUrl}/api/distance?origin={Uri.EscapeDataString(app.Origin ?? "公司")}&destination={Uri.EscapeDataString(app.Destination ?? "")}";
                            var res = await client.GetStringAsync(url);
                            var json = JObject.Parse(res);

                            km = json["distanceKm"]?.Value<double>() ?? 0;
                            minutes = json["durationMin"]?.Value<double>() ?? 0;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("⚠️ Distance API 失敗: " + ex.Message);
                        }

                        dispatch.IsLongTrip = km > 30;

                        app.DriverId = driverState.SelectedDriverId;
                        app.VehicleId = vehicleId;
                        app.isLongTrip = (app.SingleDistance ?? 0) > 30;
                        app.Status = "完成審核";
                        _db.SaveChanges();

                        bot.ReplyMessage(replyToken, $"✅ 已選擇車輛：{plateNo}");

                        var doneBubble = BuildDoneBubble(driverState.SelectedDriverName, plateNo);
                        bot.PushMessageWithJSON(uid, $"[{doneBubble}]");

                        if (_applyToApplicant.TryGetValue(app.ApplyId, out var applicantUid))
                            bot.PushMessageWithJSON(applicantUid, $"[{doneBubble}]");

                        // 從資料庫找出對應駕駛的 LineUserId
                        var driverLineId = (from d in _db.Drivers
                                            join u in _db.Users on d.UserId equals u.UserId
                                            where d.DriverId == app.DriverId && u.LineUserId != null && u.LineUserId != ""
                                            select u.LineUserId).FirstOrDefault();


                        if (!string.IsNullOrEmpty(driverLineId))
                        {
                            var notice = BuildDriverDispatchBubble(app, driverState.SelectedDriverName, plateNo, km, minutes);
                            bot.PushMessageWithJSON(driverLineId, $"[{notice}]");
                        }


                        _flow.TryRemove(uid, out _);
                        return Ok();
                    }
                }

                // ================= MESSAGE 事件 =================
                if (ev.type == "message")
                {
                    var msg = (ev.message.text ?? "").Trim();
                    var state = _flow.GetOrAdd(uid, _ => new BookingState());
                    // 全域訊息安全檢查
                    if (!IsValidUserText(msg) || ContainsSqlMeta(msg))
                    {
                        bot.ReplyMessage(replyToken, "輸入格式不正確，請重新輸入。");
                        continue;
                    }
                    // 管理員：查看待審核清單
                    if (msg == "待審核")
                    {
                        // 角色確認（以 Users.LineUserId + Role 判斷）
                        var isAdmin = _db.Users.Any(u => u.LineUserId == uid && u.Role == "Admin");
                        if (!isAdmin)
                        {
                            bot.ReplyMessage(replyToken, "您沒有權限查看待審核清單。");
                            continue;
                        }

                        // 第 1 頁
                        var bubble = BuildPendingListBubble(page: 1, pageSize: 5, _db);
                        if (bubble == null)
                        {
                            bot.ReplyMessage(replyToken, "目前沒有待審核的申請。");
                        }
                        else
                        {
                            bot.ReplyMessageWithJSON(replyToken, $"[{bubble}]");
                        }
                        continue;
                    }
                    if (msg == "我的行程")
                    {
                        // 1. 找到目前使用者
                        var user = _db.Users.FirstOrDefault(u => u.LineUserId == uid);
                        if (user == null)
                        {
                            bot.ReplyMessage(replyToken, "⚠️ 找不到您的帳號，請先完成綁定。");
                            continue;
                        }
                        //2. 找出對應的申請人
                        var applicant = _db.Applicants.FirstOrDefault(a => a.UserId == user.UserId);
                        if (applicant == null)
                        {
                            bot.ReplyMessage(replyToken, "⚠️ 找不到您的申請人資料。");
                            continue;
                        }

                        // 3. 查詢今天的申請單
                        var today = DateTime.Today;
                        var apps = _db.CarApplications
                            .Where(a => a.ApplicantId == applicant.ApplicantId &&
                            a.UseStart.Date == today)
                            .ToList();

                        // 4. 組裝回覆
                        if (!apps.Any())
                        {
                            bot.ReplyMessage(replyToken, "📌 您今天沒有申請任何行程。");
                        }
                        else
                        {
                            var lines = apps.Select(a =>
                                $"📝 申請單 {a.ApplyId}\n" +
                                $"⏰ {a.UseStart:HH:mm} - {a.UseEnd:HH:mm}\n" +
                                $"🚗 {a.Origin} → {a.Destination}");
                            var reply = "📌 您今天的行程：\n\n" + string.Join("\n\n", lines);
                            bot.ReplyMessage(replyToken, reply);
                        }
                        continue;
                    }


                    // Step 1: 開始預約
                    if (msg.Contains("預約車輛"))
                    {
                        var role = GetUserRole(uid);
                        if (role != "Applicant")
                        {
                            bot.ReplyMessage(replyToken, "⚠️ 您沒有申請派車的權限，請聯絡管理員開通帳號");
                            continue;
                        }

                        _flow[uid] = new BookingState(); // reset
                        bot.ReplyMessageWithJSON(replyToken, Step1JsonArray);
                        continue;
                    }

                    // Step 2: 預約時間
                    if (string.IsNullOrEmpty(state.ReserveTime) && (msg == "即時預約" || msg == "預訂時間"))
                    {
                        if (msg == "即時預約")
                        {
                            var now = DateTime.Now;
                            state.ReserveTime = now.ToString("yyyy/MM/dd HH:mm");
                            bot.ReplyMessage(replyToken, "請輸入用車事由");
                        }
                        else // 預訂時間 → 顯示時間選單
                        {
                            bot.ReplyMessageWithJSON(replyToken, Step2TimeJsonArray);
                        }
                        continue;
                    }

                    // 使用者點了「手動輸入」
                    if (string.IsNullOrEmpty(state.ReserveTime) && msg == "手動輸入")
                    {
                        bot.ReplyMessage(replyToken, "請輸入時間，格式：HH:mm 或 yyyy/MM/dd HH:mm（例：09:30 或 2025/09/18 09:30）");
                        continue;
                    }

                    // 使用者選了時間（08:00~17:00），或手動輸入了時間字串
                    if (string.IsNullOrEmpty(state.ReserveTime))
                    {
                        // 簡單判斷：HH:mm（08:00~17:59 都算）、或 yyyy/MM/dd HH:mm
                        DateTime parsed;
                        bool ok = false;

                        // HH:mm
                        if (System.Text.RegularExpressions.Regex.IsMatch(msg, @"^\d{2}:\d{2}$"))
                        {
                            // 以今天日期 + 使用者時分
                            var today = DateTime.Today;
                            var parts = msg.Split(':');
                            int hh, mm;
                            if (int.TryParse(parts[0], out hh) && int.TryParse(parts[1], out mm))
                            {
                                parsed = new DateTime(today.Year, today.Month, today.Day, hh, mm, 0);
                                ok = true;
                            }
                            else parsed = DateTime.Now;
                        }
                        // yyyy/MM/dd HH:mm
                        else if (DateTime.TryParse(msg, out parsed))
                        {
                            ok = true;
                        }

                        if (ok)
                        {
                            state.ReserveTime = parsed.ToString("yyyy/MM/dd HH:mm");
                            bot.ReplyMessage(replyToken, "請輸入用車事由");
                            continue;
                        }
                    }


                    // Step 3: 用車事由
                    if (!string.IsNullOrEmpty(state.ReserveTime) &&
                        string.IsNullOrEmpty(state.Reason) &&
                        !msg.EndsWith("人") && msg != "確認" && msg != "取消")
                    {
                        state.Reason = msg;
                        bot.ReplyMessageWithJSON(replyToken, Step3JsonArray);
                        continue;
                    }

                    // Step 4: 人數
                    if (!string.IsNullOrEmpty(state.Reason) &&
                        string.IsNullOrEmpty(state.PassengerCount) &&
                        msg.EndsWith("人"))
                    {
                        state.PassengerCount = msg;
                        bot.ReplyMessage(replyToken, "請輸入出發地點");
                        continue;
                    }

                    // Step 5: 出發地
                    if (!string.IsNullOrEmpty(state.PassengerCount) &&
                        string.IsNullOrEmpty(state.Origin) &&
                        msg != "確認" && msg != "取消")
                    {
                        var result = await ValidateAddressAsync(msg);
                        if (!result.ok)
                        {
                            bot.ReplyMessage(replyToken, result.error);
                            continue;
                        }

                        state.Origin = result.formatted;
                        bot.ReplyMessage(replyToken, $"✅ 出發地已設定：{result.formatted}\n請輸入前往地點");
                        continue;
                    }

                    // Step 6: 目的地
                    if (!string.IsNullOrEmpty(state.Origin) &&
                        string.IsNullOrEmpty(state.Destination) &&
                        msg != "確認" && msg != "取消")
                    {
                        var result = await ValidateAddressAsync(msg);
                        if (!result.ok)
                        {
                            bot.ReplyMessage(replyToken, result.error);
                            continue;
                        }

                        state.Destination = result.formatted;
                        bot.ReplyMessageWithJSON(replyToken, Step6bTripJsonArray);
                        continue;
                    }


                    // Step 6b: 單程/來回
                    if (!string.IsNullOrEmpty(state.Destination) &&
                        string.IsNullOrEmpty(state.TripType) &&
                        (msg == "單程" || msg == "來回"))
                    {
                        state.TripType = (msg == "單程") ? "single" : "round";

                        // 確認卡片
                        var safeReserveTime = Safe(state.ReserveTime);
                        var safeReason = Safe(state.Reason);
                        var safePax = Safe(state.PassengerCount);
                        var safeOrigin = Safe(state.Origin);
                        var safeDest = Safe(state.Destination);
                        string confirmBubble = $@"
{{
  ""type"": ""flex"",
  ""altText"": ""申請派車資訊"",
  ""contents"": {{
    ""type"": ""bubble"",
    ""body"": {{
      ""type"": ""box"",
      ""layout"": ""vertical"",
      ""spacing"": ""md"",
      ""contents"": [
        {{ ""type"": ""text"", ""text"": ""申請派車資訊"", ""weight"": ""bold"", ""size"": ""lg"" }},
        {{ ""type"": ""text"", ""text"": ""■ 預約時間：{state.ReserveTime}"" }},
        {{ ""type"": ""text"", ""text"": ""■ 用車事由：{state.Reason}"" }},
        {{ ""type"": ""text"", ""text"": ""■ 乘客人數：{state.PassengerCount}"" }},
        {{ ""type"": ""text"", ""text"": ""■ 出發地點：{state.Origin}"" }},
        {{ ""type"": ""text"", ""text"": ""■ 前往地點：{state.Destination}"" }}
      ]
    }},
    ""footer"": {{
      ""type"": ""box"",
      ""layout"": ""horizontal"",
      ""contents"": [
        {{ ""type"": ""button"", ""style"": ""secondary"", ""action"": {{ ""type"": ""message"", ""label"": ""取消"", ""text"": ""取消"" }} }},
        {{ ""type"": ""button"", ""style"": ""primary"", ""action"": {{ ""type"": ""message"", ""label"": ""確認"", ""text"": ""確認"" }} }}
      ]
    }}
  }}
}}";
                        bot.ReplyMessageWithJSON(replyToken, $"[{confirmBubble}]");
                        continue;
                    }

                    // Step 7: 取消
                    if (msg == "取消")
                    {
                        bot.ReplyMessage(replyToken, "❌ 已取消派車申請。");
                        _flow.TryRemove(uid, out _);
                        continue;
                    }

                    // Step 8: 確認 → 存DB & 通知管理員
                    if (msg == "確認")
                    {
                        var role = GetUserRole(uid);
                        if (role != "Applicant")
                        {
                            bot.ReplyMessage(replyToken, "⚠️ 您沒有建立派車申請的權限");
                            continue;
                        }


                        // 先呼叫 Distance API 算距離與時間
                        double km = 0, minutes = 0;
                        try
                        {
                            using var client = new HttpClient();
                            var url = $"{_baseUrl}/api/distance?origin={Uri.EscapeDataString(state.Origin ?? "公司")}&destination={Uri.EscapeDataString(state.Destination ?? "")}";
                            var res = await client.GetStringAsync(url);

                            Console.WriteLine("?? Response: " + res);

                            var json = JObject.Parse(res);

                            // 直接取出 distanceKm 和 durationMin
                            km = json["distanceKm"]?.Value<double>() ?? 0;
                            minutes = json["durationMin"]?.Value<double>() ?? 0;

                            Console.WriteLine($"?? API 回傳距離: {km} 公里, 時間: {minutes} 分鐘");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("⚠️ Distance API 失敗: " + ex.Message);
                        }
                        // 把 UseStart 決定好：優先用使用者選/輸入，否則用現在
                        DateTime start;
                        if (!string.IsNullOrEmpty(state.ReserveTime))
                        {
                            DateTime tmp;
                            if (DateTime.TryParse(state.ReserveTime, out tmp))
                                start = tmp;
                            else
                            {
                                // 若是只有 HH:mm（被你上面格式化成完整字串就不會來這裡；這裡是保險）
                                var today = DateTime.Today;
                                start = new DateTime(today.Year, today.Month, today.Day, 8, 0, 0);
                            }
                        }
                        else
                        {
                            start = DateTime.Now;
                        }
                        // 先找到 User
                        var user = _db.Users.FirstOrDefault(u => u.LineUserId == uid);
                        if (user == null)
                        {
                            bot.ReplyMessage(replyToken, "⚠️ 找不到您的帳號，請先完成綁定。");
                            return Ok();
                        }

                        // 再找到 Applicant
                        var applicant = _db.Applicants.FirstOrDefault(a => a.UserId == user.UserId);
                        if (applicant == null)
                        {
                            bot.ReplyMessage(replyToken, "⚠️ 找不到申請人資料。");
                            return Ok();
                        }

                        // minutes 是 Google API 回來的「單程」分鐘數
                        double effectiveMinutes = minutes;

                        // 如果是 round 行程，就乘 2
                        if (state.TripType == "round")
                            effectiveMinutes = minutes * 2;
                        // 建立申請單 (先存 DB)
                        var app = new CarApplication
                        {
                            ApplyFor = "申請人",
                            VehicleType = "汽車",
                            PurposeType = "公務車(不可選車)",
                            ReasonType = "公務用",
                            PassengerCount = int.TryParse(state.PassengerCount?.Replace("人", ""), out var n) ? n : 1,
                            ApplyReason = state.Reason ?? string.Empty,
                            Origin = state.Origin ?? "公司",
                            Destination = state.Destination ?? "",
                            UseStart = start,
                            UseEnd = start.AddMinutes(effectiveMinutes),
                            TripType = state.TripType ?? "single",
                            ApplicantId = applicant.ApplicantId,
                            Status = "待審核",

                            // 這邊直接存距離 & 時間
                            SingleDistance = (decimal)km,
                            SingleDuration = ToHourMinuteString(minutes),
                            RoundTripDistance = (decimal)(km * 2),
                            RoundTripDuration = ToHourMinuteString(minutes * 2),
                            isLongTrip = km > 30
                        };
                        _db.CarApplications.Add(app);
                        _db.SaveChanges();

                        var dispatch = new Dispatch
                        {
                            ApplyId = app.ApplyId,
                            DispatchStatus = "待指派",
                            CreatedAt = DateTime.Now,
                            IsLongTrip = km > 30
                        };
                        _db.Dispatches.Add(dispatch);
                        _db.SaveChanges();

                        _applyToApplicant[app.ApplyId] = uid;

                        // 推播給管理員
                        var adminIds = _db.Users
                            .Where(u => u.Role == "Admin" && u.LineUserId != null && u.LineUserId != "")
                            .Select(u => u.LineUserId)
                            .ToList(); var adminFlex = BuildAdminFlexBubble(app);
                        foreach (var aid in adminIds)
                            bot.PushMessageWithJSON(aid, $"[{adminFlex}]");

                        bot.ReplyMessage(replyToken, "✅ 已送出派車申請，等待審核。");
                        _flow.TryRemove(uid, out _);
                        continue;
                    }
                    // ================= 管理員審核 =================
                    if (msg.StartsWith("同意申請") || msg.StartsWith("拒絕申請"))
                    {
                        var role = GetUserRole(uid);
                        if (role != "Admin")
                        {
                            bot.ReplyMessage(replyToken, "⚠️ 您沒有審核的權限");
                            continue;
                        }
                        if (msg.StartsWith("同意申請"))
                        {
                            if (!TryParseId(msg, out var applyId))
                            {
                                bot.ReplyMessage(replyToken, "❗ 指令格式錯誤");
                                continue;
                            }

                            var app = _db.CarApplications.FirstOrDefault(a => a.ApplyId == applyId);
                            if (app == null)
                            {
                                bot.ReplyMessage(replyToken, "⚠️ 找不到該申請單");
                                continue;
                            }

                            // 顯示「選擇駕駛人」卡片
                            var selectDriverBubble = BuildDriverSelectBubble(applyId, _db);
                            bot.ReplyMessageWithJSON(replyToken, $"[{selectDriverBubble}]");
                            continue;
                        }

                        if (msg.StartsWith("拒絕申請"))
                        {
                            if (!TryParseId(msg, out var applyId))
                            {
                                bot.ReplyMessage(replyToken, "❗ 指令格式錯誤");
                                continue;
                            }

                            var app = _db.CarApplications.FirstOrDefault(a => a.ApplyId == applyId);
                            if (app == null)
                            {
                                bot.ReplyMessage(replyToken, "⚠️ 找不到該申請單");
                                continue;
                            }

                            app.Status = "已拒絕";
                            _db.SaveChanges();

                            // 通知申請人
                            if (_applyToApplicant.TryGetValue(applyId, out var applicantUid))
                            {
                                bot.PushMessage(applicantUid,
                                    $"❌ 您的派車申請已被拒絕\n事由：{app.ApplyReason}\n地點：{app.Destination}");
                            }

                            bot.ReplyMessage(replyToken, "✅ 已拒絕該申請。");
                            continue;
                        }
                        //================= 駕駛開始行程 =================
                        if (msg.Contains("開始行程"))
                        {
                            // 驗證角色
                            if (dbUser.Role != "Driver")
                            {
                                bot.ReplyMessage(replyToken, "⚠️ 您不是駕駛人，不能開始行程");
                                continue;
                            }

                            // 找到駕駛的資料
                            var driver = _db.Drivers.FirstOrDefault(d => d.UserId == dbUser.UserId);
                            if (driver == null)
                            {
                                bot.ReplyMessage(replyToken, "⚠️ 找不到您的駕駛資料");
                                continue;
                            }

                            var today = DateTime.Today;
                            var now = DateTime.Now;

                            // 找出今天最新一張派車單（狀態為「已派車」但未開始）
                            var dispatch = _db.Dispatches
                                .Where(d => d.DriverId == driver.DriverId &&
                                            d.DispatchStatus == "已派車" &&
                                            d.StartTime.HasValue &&
                                            d.StartTime.Value.Date == today)
                                .OrderByDescending(d => d.DispatchId)
                                .FirstOrDefault();

                            if (dispatch == null)
                            {
                                bot.ReplyMessage(replyToken, "⚠️ 今天沒有可執行的派車任務");
                                continue;
                            }

                            // 更新派車單狀態
                            dispatch.DispatchStatus = "執行中";
                            dispatch.StartTime = now;
                            _db.SaveChanges();

                            bot.ReplyMessage(replyToken, $"✅ 行程已開始\n任務單號：{dispatch.DispatchId}\n開始時間：{now:HH:mm}");
                            continue;
                        }

                        if (msg.Equals("開始行程", StringComparison.OrdinalIgnoreCase))
                        {
                            if (dbUser.Role != "Driver")
                            {
                                bot.ReplyMessage(replyToken, "⚠️ 您不是駕駛人，不能操作行程");
                                continue;
                            }

                            var driver = _db.Drivers.FirstOrDefault(d => d.UserId == dbUser.UserId);
                            if (driver == null)
                            {
                                bot.ReplyMessage(replyToken, "⚠️ 找不到您的駕駛資料");
                                continue;
                            }

                            var now = DateTime.Now;

                            // 檢查是否有執行中的任務
                            var running = _db.Dispatches
                                .Where(d => d.DriverId == driver.DriverId && d.DispatchStatus == "執行中")
                                .OrderByDescending(d => d.DispatchId)
                                .FirstOrDefault();

                            if (running != null)
                            {
                                // 🔻 已在執行 → 按下就結束
                                running.DispatchStatus = "已完成";
                                running.EndTime = now;
                                _db.SaveChanges();

                                bot.ReplyMessage(replyToken, $"✅ 行程已完成\n任務單號：{running.DispatchId}\n結束時間：{now:HH:mm}");
                                continue;
                            }

                            // 沒有執行中的 → 檢查有沒有待開始的任務
                            var pending = _db.Dispatches
                                .Where(d => d.DriverId == driver.DriverId && d.DispatchStatus == "已派車" && !d.StartTime.HasValue)
                                .OrderByDescending(d => d.DispatchId)
                                .FirstOrDefault();

                            if (pending == null)
                            {
                                bot.ReplyMessage(replyToken, "⚠️ 您目前沒有可執行的派車任務");
                                continue;
                            }

                            // 🔻 待開始 → 按下就開始
                            pending.DispatchStatus = "執行中";
                            pending.StartTime = now;
                            _db.SaveChanges();

                            bot.ReplyMessage(replyToken, $"✅ 行程已開始\n任務單號：{pending.DispatchId}\n開始時間：{now:HH:mm}");
                            continue;
                        }


                        // 其它訊息：回聲
                        bot.ReplyMessage(replyToken, $"你剛剛說：{msg}");
                    }
                }
            }

            return Ok();
        }
        #endregion

        #region 檢查當下使用者角色
        // 共用方法：檢查角色
        private string GetUserRole(string lineUserId)
        {
            var user = _db.Users.FirstOrDefault(u => u.LineUserId == lineUserId);
            return user?.Role ?? "";
        }
        #endregion

        #region 地址轉換與驗證
        // ====== 工具方法：驗證地址 ======
        private async Task<(bool ok, string formatted, double lat, double lng, string error)>
            ValidateAddressAsync(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return (false, "", 0, 0, "⚠️ 請輸入地址");

            try
            {
                var apiKey = _config["GoogleMaps:ApiKey"]; // 需要 IConfiguration
                var url = $"https://maps.googleapis.com/maps/api/geocode/json?address={Uri.EscapeDataString(input)}&region=tw&key={apiKey}";

                using var client = new HttpClient();
                var res = await client.GetStringAsync(url);
                var geo = JObject.Parse(res);

                if (geo["status"]?.ToString() != "OK")
                    return (false, "", 0, 0, "⚠️ 找不到此地址，請重新輸入");

                var formatted = geo["results"]?[0]?["formatted_address"]?.ToString();
                var location = geo["results"]?[0]?["geometry"]?["location"];
                double lat = location?["lat"]?.Value<double>() ?? 0;
                double lng = location?["lng"]?.Value<double>() ?? 0;

                return (true, formatted ?? input, lat, lng, "");
            }
            catch (Exception ex)
            {
                return (false, "", 0, 0, "⚠️ 驗證地址失敗：" + ex.Message);
            }
        }
        #endregion

        #region 管理員審核卡片
        //管理員審核清單卡片
        private static string BuildPendingListBubble(int page, int pageSize, ApplicationDbContext db)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 5;

            var q = db.CarApplications
                .Where(a => a.Status == "待審核")
                .OrderBy(a => a.UseStart);

            var total = q.Count();
            if (total == 0) return null;

            var items = q.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            // 每筆一個盒子 + 按鈕
            var cardContents = string.Join(",\n", items.Select(a => $@"
{{
  ""type"": ""box"",
  ""layout"": ""vertical"",
  ""margin"": ""md"",
  ""spacing"": ""xs"",
  ""borderWidth"": ""1px"",
  ""borderColor"": ""#dddddd"",
  ""cornerRadius"": ""md"",
  ""paddingAll"": ""10px"",
  ""contents"": [
    {{ ""type"": ""text"", ""text"": ""申請單 #{a.ApplyId}"", ""weight"": ""bold"" }},
    {{ ""type"": ""text"", ""text"": ""時間：{a.UseStart:yyyy/MM/dd HH:mm} - {a.UseEnd:HH:mm}"", ""size"": ""sm"" }},
    {{ ""type"": ""text"", ""text"": ""路線：{(a.Origin ?? "公司")} → {a.Destination}"", ""size"": ""sm"", ""wrap"": true }},
    {{ ""type"": ""text"", ""text"": ""人數：{a.PassengerCount}、行程：{(a.TripType == "round" ? "來回" : "單程")}"", ""size"": ""sm"" }},
    {{ ""type"": ""box"", ""layout"": ""horizontal"", ""spacing"": ""md"", ""margin"": ""sm"", ""contents"": [
      {{
        ""type"": ""button"",
        ""style"": ""primary"",
        ""height"": ""sm"",
        ""action"": {{
          ""type"": ""postback"",
          ""label"": ""同意"",
          ""data"": ""action=reviewApprove&applyId={a.ApplyId}""
        }}
      }},
      {{
        ""type"": ""button"",
        ""style"": ""secondary"",
        ""height"": ""sm"",
        ""action"": {{
          ""type"": ""postback"",
          ""label"": ""拒絕"",
          ""data"": ""action=reviewReject&applyId={a.ApplyId}""
        }}
      }}
    ]}}
  ]
}}"));

            var totalPages = (int)Math.Ceiling(total / (double)pageSize);
            var hasPrev = page > 1;
            var hasNext = page < totalPages;

            var footerButtons = new List<string>();
            if (hasPrev)
            {
                footerButtons.Add(@$"{{
          ""type"": ""button"",
          ""style"": ""secondary"",
          ""action"": {{ ""type"": ""postback"", ""label"": ""上一頁"", ""data"": ""action=reviewListPage&page={page - 1}"" }}
        }}");
            }
            if (hasNext)
            {
                footerButtons.Add(@$"{{
          ""type"": ""button"",
          ""style"": ""secondary"",
          ""action"": {{ ""type"": ""postback"", ""label"": ""下一頁"", ""data"": ""action=reviewListPage&page={page + 1}"" }}
        }}");
            }

            var footer = footerButtons.Count > 0
                ? string.Join(",", footerButtons)
                : @"{ ""type"": ""text"", ""text"": ""已到清單底部"", ""align"": ""center"", ""size"": ""sm"", ""color"": ""#888888"" }";

            // Flex bubble
            var bubble = $@"
{{
  ""type"": ""flex"",
  ""altText"": ""待審核清單"",
  ""contents"": {{
    ""type"": ""bubble"",
    ""size"": ""mega"",
    ""body"": {{
      ""type"": ""box"",
      ""layout"": ""vertical"",
      ""spacing"": ""md"",
      ""contents"": [
        {{ ""type"": ""text"", ""text"": ""待審核清單"", ""weight"": ""bold"", ""size"": ""lg"" }},
        {cardContents}
      ]
    }},
    ""footer"": {{
      ""type"": ""box"",
      ""layout"": ""horizontal"",
      ""spacing"": ""md"",
      ""contents"": [
        {footer}
      ]
    }}
  }}
}}";

            return bubble;
        }
        #endregion

        #region 通知

        //申請人通知卡片
        private static string BuildAdminFlexBubble(CarApplication app) => $@"
{{
  ""type"": ""flex"",
  ""altText"": ""派車申請"",
  ""contents"": {{
    ""type"": ""bubble"",
    ""body"": {{
      ""type"": ""box"",
      ""layout"": ""vertical"",
      ""contents"": [
        {{ ""type"": ""text"", ""text"": ""派車申請"", ""weight"": ""bold"", ""size"": ""lg"" }},
        {{ ""type"": ""text"", ""text"": ""■ 申請人：{app.ApplyFor}"" }},        
        {{ ""type"": ""text"", ""text"": ""■ 用車事由：{app.ApplyReason}"" }},
        {{ ""type"": ""text"", ""text"": ""■ 乘客人數：{app.PassengerCount}"" }},
        {{ ""type"": ""text"", ""text"": ""■ 派車時間：{app.UseStart:yyyy/MM/dd HH:mm}"" }},
        {{ ""type"": ""text"", ""text"": ""■ 前往地點：{app.Destination}"" }}
      ]
    }},
    ""footer"": {{
      ""type"": ""box"",
      ""layout"": ""horizontal"",
      ""contents"": [
        {{ ""type"": ""button"", ""style"": ""secondary"", ""action"": {{ ""type"": ""message"", ""label"": ""拒絕"", ""text"": ""拒絕申請 {app.ApplyId}"" }} }},
        {{ ""type"": ""button"", ""style"": ""primary"",   ""action"": {{ ""type"": ""message"", ""label"": ""同意"", ""text"": ""同意申請 {app.ApplyId}"" }} }}
      ]
    }}
  }}
}}";
        //選擇司機卡片
        private static string BuildDriverSelectBubble(int applyId, ApplicationDbContext db)
        {
            var now = DateTime.Now;

            var drivers = db.Drivers
                .Where(d =>!d.IsAgent &&
                    //沒有正在出勤
                    !db.Dispatches.Any(dis =>
                        dis.DriverId == d.DriverId &&
                        dis.DispatchStatus == "已派車" &&
                        dis.StartTime <= now &&
                        dis.EndTime >= now)

                  
                       
                )
                .Select(d => new { d.DriverId, d.DriverName })
                .Take(5)
                .ToList();


            var btns = string.Join(",\n        ", drivers.Select(d =>
                $@"{{
            ""type"": ""button"",
            ""action"": {{
                ""type"": ""postback"",
                ""label"": ""{d.DriverName}"",
                ""data"": ""action=assignDriver&applyId={applyId}&driverId={d.DriverId}&driverName={d.DriverName}""
            }}
        }}"));

            return $@"
{{
  ""type"": ""flex"",
  ""altText"": ""選擇駕駛人"",
  ""contents"": {{
    ""type"": ""bubble"",
    ""body"": {{
      ""type"": ""box"",
      ""layout"": ""vertical"",
      ""contents"": [
        {{ ""type"": ""text"", ""text"": ""請選擇駕駛人"", ""weight"": ""bold"", ""size"": ""lg"" }},
        {btns}
      ]
    }}
  }}
}}";
        }
        //選擇車輛卡片
        private static string BuildCarSelectBubble(int applyId, ApplicationDbContext db)
        {
            var now = DateTime.Now;

            // 過濾掉正在使用中的車輛
            var cars = db.Vehicles
                .Where(v => v.Status == "可用"&&
                !db.Dispatches.Any(dis =>
                    dis.VehicleId == v.VehicleId &&
                    dis.DispatchStatus == "已派車" &&
                    dis.StartTime <= now &&
                    dis.EndTime >= now))
                .Select(v => new { v.VehicleId, v.PlateNo })
                .Take(5)
                .ToList();

            var btns = string.Join(",\n        ", cars.Select(c =>
                $@"{{
            ""type"": ""button"",
            ""action"": {{
                ""type"": ""postback"",
                ""label"": ""{c.PlateNo}"",
                ""data"": ""action=assignVehicle&applyId={applyId}&vehicleId={c.VehicleId}&plateNo={c.PlateNo}""
            }}
        }}"));

            return $@"
{{
  ""type"": ""flex"",
  ""altText"": ""選擇車輛"",
  ""contents"": {{
    ""type"": ""bubble"",
    ""body"": {{
      ""type"": ""box"",
      ""layout"": ""vertical"",
      ""contents"": [
        {{ ""type"": ""text"", ""text"": ""請選擇車輛"", ""weight"": ""bold"", ""size"": ""lg"" }},
        {btns}
      ]
    }}
  }}
}}";
        }

        //通知申請人已安排駕駛人員
        private static string BuildDoneBubble(string driverName, string carNo) => $@"
{{
  ""type"": ""flex"",
  ""altText"": ""已安排駕駛人員"",
  ""contents"": {{
    ""type"": ""bubble"",
    ""body"": {{
      ""type"": ""box"",
      ""layout"": ""vertical"",
      ""contents"": [
        {{ ""type"": ""text"", ""text"": ""已安排駕駛人員"", ""weight"": ""bold"", ""size"": ""lg"" }},
        {{ ""type"": ""text"", ""text"": ""■ 駕駛人：{driverName}"" }},
        {{ ""type"": ""text"", ""text"": ""■ 使用車輛：{carNo}"" }}
      ]
    }}
  }}
}}";

        // 駕駛—派車通知
        private static string BuildDriverDispatchBubble(CarApplication app, string driverName, string carNo, double km, double minutes)
        {
            // 根據行程類型決定顯示距離/時間
            bool isRound = app.TripType == "round";

            double showKm = isRound ? km * 2 : km;
            double showMinutes = isRound ? minutes * 2 : minutes;

            string distanceText = $"■ 距離：約 {showKm:F1} 公里";
            string durationText = $"■ 車程：約 {ToHourMinuteString(showMinutes)}";
            var safeApplyFor = Safe(app.ApplyFor);
            var safeOrigin = Safe(app.Origin);
            var safeDest = Safe(app.Destination);
            return $@"
{{
  ""type"": ""flex"",
  ""altText"": ""派車通知"",
  ""contents"": {{
    ""type"": ""bubble"",
    ""body"": {{
      ""type"": ""box"",
      ""layout"": ""vertical"",
      ""contents"": [
        {{ ""type"": ""text"", ""text"": ""🚗 派車通知"", ""weight"": ""bold"", ""size"": ""lg"" }},
        {{ ""type"": ""text"", ""text"": ""■ 任務單號：{app.ApplyId}"" }},
        {{ ""type"": ""text"", ""text"": ""■ 預約時間：{app.UseStart:yyyy/MM/dd HH:mm}"" }},
        {{ ""type"": ""text"", ""text"": ""■ 申請人：{app.ApplyFor ?? "未知"}"" }},
        {{ ""type"": ""text"", ""text"": ""■ 駕駛人：{driverName}"" }},
        {{ ""type"": ""text"", ""text"": ""■ 車輛：{carNo}"" }},
        {{ ""type"": ""text"", ""text"": ""{distanceText}"" }},
        {{ ""type"": ""text"", ""text"": ""{durationText}"" }},
        {{ ""type"": ""text"", ""text"": ""■ 乘客人數：{app.PassengerCount}"" }},
        {{ ""type"": ""text"", ""text"": ""■ 上車地點：{app.Origin ?? "公司"}"" }},
        {{ ""type"": ""text"", ""text"": ""■ 前往地點：{app.Destination}"" }},
        {{ ""type"": ""separator"", ""margin"": ""md"" }},
        {{ ""type"": ""text"", ""text"": ""請即刻前往指定地點，若有其他問題請撥02-12345678，謝謝!"",
           ""wrap"": true, ""size"": ""sm"", ""color"": ""#555555"", ""margin"": ""md"" }}
      ]
    }}
  }}
}}";
        }
        // 駕駛—開始行程確認
        private static string BuildStartedBubble(Dispatch d) => $@"
{{
  ""type"": ""flex"",
  ""altText"": ""行程已開始"",
  ""contents"": {{
    ""type"": ""bubble"",
    ""body"": {{
      ""type"": ""box"", ""layout"": ""vertical"",
      ""contents"": [
        {{ ""type"": ""text"", ""text"": ""行程已開始"", ""weight"": ""bold"", ""size"": ""lg"" }},
        {{ ""type"": ""text"", ""text"": ""出發時間：{DateTime.Now:HH:mm}"" }}
      ]
    }}
  }}
}}";

        // 駕駛—完成行程確認
        private static string BuildFinishedBubble(Dispatch d) => $@"
{{
  ""type"": ""flex"",
  ""altText"": ""行程已完成"",
  ""contents"": {{
    ""type"": ""bubble"",
    ""body"": {{
      ""type"": ""box"", ""layout"": ""vertical"",
      ""contents"": [
        {{ ""type"": ""text"", ""text"": ""行程已完成"", ""weight"": ""bold"", ""size"": ""lg"" }},
        {{ ""type"": ""text"", ""text"": ""結束時間：{DateTime.Now:HH:mm}"" }}
      ]
    }}
  }}
}}";
        #endregion

        #region 轉換工具
        // 解析申請單 ID
        private static bool TryParseId(string msg, out int id)
        {
            id = 0;
            var parts = msg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 && int.TryParse(parts[^1], out id);
        }
        // 公里轉文字
        private static string ToHourMinuteString(double minutes)
        {
            int totalMinutes = (int)Math.Round(minutes);
            int hours = totalMinutes / 60;
            int mins = totalMinutes % 60;

            if (hours > 0)
                return $"{hours} 小時 {mins} 分鐘";
            else
                return $"{mins} 分鐘";
        }
        #endregion

        #region 避免惡意攻擊
        // 允許的文字（白名單）＋長度限制
        private static bool IsValidUserText(string s, int maxLen = 300)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            if (s.Length > maxLen) return false;
            // 中文、英文、數字、常見標點與空白
            var regex = new System.Text.RegularExpressions.Regex(@"^[\u4e00-\u9fff\w\-\.,:\/\s()]{1,}$");
            return regex.IsMatch(s);
        }

        // 粗略攔截疑似 SQL 關鍵片段（第二道防線：記錄/阻擋）
        private static bool ContainsSqlMeta(string s)
        {
            var suspicious = new[] { "--", ";--", "/*", "*/", ";", " xp_", " drop ", " truncate ", " insert ", " delete ", " update ", " exec ", " sp_" };
            var lower = s.ToLowerInvariant();
            return suspicious.Any(p => lower.Contains(p));
        }

        // 安全輸出到 JSON 文字（避免把使用者輸入直接插入 Flex JSON）
        private static string Safe(string? raw)
        {
            // 轉為安全 JSON 字面值再去除最外層引號 → 適合放到 text 欄位
            var json = Newtonsoft.Json.JsonConvert.ToString(raw ?? "");
            return json.Length >= 2 ? json.Substring(1, json.Length - 2) : "";
        }

        // 時間字串解析（接受 HH:mm 或 yyyy/MM/dd HH:mm）
        private static bool TryParseUserTime(string input, out DateTime result)
        {
            result = DateTime.MinValue;
            if (System.Text.RegularExpressions.Regex.IsMatch(input, @"^\d{2}:\d{2}$"))
            {
                var parts = input.Split(':');
                if (int.TryParse(parts[0], out var hh) && int.TryParse(parts[1], out var mm) &&
                    hh >= 0 && hh <= 23 && mm >= 0 && mm <= 59)
                {
                    var today = DateTime.Today;
                    result = new DateTime(today.Year, today.Month, today.Day, hh, mm, 0);
                    return true;
                }
                return false;
            }
            return DateTime.TryParse(input, out result);
        }
        #endregion

    }
}
