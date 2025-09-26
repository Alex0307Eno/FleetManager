using Cars.Data;
using Cars.Features.CarApplications;
using Cars.Models;
using Cars.Services;
using DocumentFormat.OpenXml.Bibliography;
using isRock.LIFF;
using isRock.LineBot;
using LineBotDemo.Services;
using LineBotService.Helpers;
using LineBotService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;



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
        // 暫存綁定流程狀態：key=LineUserId, value=step
        private static readonly ConcurrentDictionary<string, string> _bindingStep = new();
        // 限流器：每個 userId 每分鐘 10 次
        private static readonly RateLimiter _rateLimiter = new RateLimiter(100, 60);
        private readonly HttpClient _http; private readonly string _apiBaseUrl;
        private Bot _bot;              // 全域 Bot 物件
        private string? _replyToken;   // 全域 replyToken
        private readonly CarApplicationService _carAppService;
        private readonly DispatchService _dispatchService;

        public LineBotController(IHttpClientFactory httpFactory, IConfiguration config, ApplicationDbContext db,RichMenuService richMenuService, CarApplicationService carAppService, DispatchService dispatchService)
        {
            _http = httpFactory.CreateClient();
            _token = config["LineBot:ChannelAccessToken"];
            _baseUrl = config["AppBaseUrl"];
            _db = db;
            _config = config;
            _richMenuService = richMenuService;
            _bot = new Bot(_token);
            _carAppService = carAppService;
            _dispatchService = dispatchService;


        }
        #region 暫存方法

        // 對話進度暫存
        private static readonly ConcurrentDictionary<string, BookingState> _flow = new();

        // 把「申請單 ApplyId 對應 申請人 LINE userId」暫存起來，方便審核後通知申請人
        private static readonly ConcurrentDictionary<int, string> _applyToApplicant = new();
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
                try {
                    var replyToken = ev.replyToken;
                    var uid = ev.source.userId ?? "unknown";
                    var msg = ev.type == "message" ? (ev.message.text ?? "").Trim() : "";
                    // 檢查速率限制
                    if (!_rateLimiter.IsAllowed(uid))
                    {
                        bot.ReplyMessage(ev.replyToken, "⚠️ 請求過於頻繁，請稍後再試");
                        continue;
                    }
                    var botClient = new Bot(_token);

                    //// 先做通用過濾
                    //if (!InputSanitizer.IsSafeText(msg))
                    //{
                    //    Console.WriteLine($"[WARN] Unsafe user text from {uid}: {msg}");
                    //    bot.ReplyMessage(replyToken, "輸入包含不允許的字元或格式，請檢查後重試。");
                    //    continue;
                    //}


                    
                    var dbUser = await _db.Users.FirstOrDefaultAsync(u => u.LineUserId == uid);
                    if (dbUser == null)
                    {
                        // Step 1: 使用者輸入「綁定帳號」
                        if (msg == "綁定帳號")
                        {
                            var state = _flow.GetOrAdd(uid, _ => new BookingState());
                            state.Reason = null;          // 當帳號暫存用
                            state.PassengerCount = null;  // 當密碼暫存用
                            bot.ReplyMessage(replyToken, "🔑 請輸入您的帳號：");
                            continue;
                        }

                        // Step 2: 如果 state.Reason 還沒存 → 表示在等帳號
                        if (_flow.TryGetValue(uid, out var bindState) && string.IsNullOrEmpty(bindState.Reason))
                        {
                            bindState.Reason = msg; // 暫存帳號
                            bot.ReplyMessage(replyToken, "📌 請輸入您的密碼：");
                            continue;
                        }

                        // Step 3: 如果 state.Reason 有帳號但 Password 還沒存 → 表示在等密碼
                        if (_flow.TryGetValue(uid, out bindState)
                            && !string.IsNullOrEmpty(bindState.Reason)
                            && !bindState.PassengerCount.HasValue)
                        {
                            var account = bindState.Reason;
                            var password = msg;

                            var user = await _db.Users.FirstOrDefaultAsync(u => u.Account == account);
                            if (user == null)
                            {
                                bot.ReplyMessage(replyToken, "⚠️ 找不到該帳號，請重新輸入「綁定帳號」開始。");
                                _flow.TryRemove(uid, out _);
                                continue;
                            }

                            bool valid = false;
                            if (!string.IsNullOrEmpty(user.PasswordHash) && user.PasswordHash.StartsWith("$2"))
                                valid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
                            else if (user.PasswordHash == password) // 舊明碼
                                valid = true;

                            if (!valid)
                            {
                                bot.ReplyMessage(replyToken, "⚠️ 密碼錯誤，請重新輸入「綁定帳號」開始。");
                                _flow.TryRemove(uid, out _);
                                continue;
                            }

                            // 綁定成功
                            user.LineUserId = uid;
                            if (!TrySave(replyToken)) continue; _flow.TryRemove(uid, out _);

                            bot.ReplyMessage(replyToken, $"✅ 帳號綁定成功！歡迎 {user.DisplayName ?? user.Account}");

                            // 自動綁定 RichMenu
                            await _richMenuService.BindUserToRoleAsync(uid, user.Role ?? "Applicant");
                            continue;
                        }

                        // 尚未綁定，統一提示
                        bot.ReplyMessage(replyToken,
                            "⚠️ 您的 LINE 尚未綁定系統帳號\n" +
                            "👉 請先輸入「綁定帳號」");
                        continue;
                    }
                    // 使用者要求解除綁定 → 進入確認流程
                    if (msg == "解除綁定")
                    {
                        bot.ReplyMessage(replyToken,
                            "⚠️ 您確定要解除綁定嗎？\n回覆「是」進行解除，回覆「否」取消操作。");

                        // 在 _flow 紀錄一個狀態，讓下一步判斷
                        var state = _flow.GetOrAdd(uid, _ => new BookingState());
                        state.Reason = "UnbindConfirm"; // 用 Reason 當暫存狀態
                        continue;
                    }

                    // 第二步：確認是否解除
                    if (_flow.TryGetValue(uid, out var state2) && state2.Reason == "UnbindConfirm")
                    {
                        if (msg == "是")
                        {
                            var user = await _db.Users.FirstOrDefaultAsync(u => u.LineUserId == uid);
                            if (user == null)
                            {
                                bot.ReplyMessage(replyToken, "⚠️ 您目前沒有綁定任何帳號。");
                            }
                            else
                            {
                                user.LineUserId = null;
                                if (!TrySave(replyToken)) continue;

                                bot.ReplyMessage(replyToken, $"🔓 您的帳號 {user.Account} 已成功解除綁定。");

                                // 解除後改綁 Guest RichMenu
                                await _richMenuService.BindUserToRoleAsync(uid, "Guest");
                            }

                            _flow.TryRemove(uid, out _); // 清掉狀態
                            continue;
                        }
                        else if (msg == "否")
                        {
                            bot.ReplyMessage(replyToken, "❌ 已取消解除綁定。");
                            _flow.TryRemove(uid, out _);
                            continue;
                        }
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
                                var role = await _db.Users
                                    .Where(u => u.LineUserId == userId)
                                    .Select(u => u.Role)
                                    .FirstOrDefaultAsync();

                                // 如果沒有，嘗試從 LineUsers 表查
                                if (string.IsNullOrEmpty(role))
                                {
                                    role = await _db.Users
                                           .Where(u => u.LineUserId == userId)
                                           .Select(u => u.Role)
                                           .FirstOrDefaultAsync();
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
                        if (msg == "我的角色")
                        {
                            // 找出使用者
                            var user = await _db.Users.FirstOrDefaultAsync(u => u.LineUserId == uid);
                            if (user == null)
                            {
                                bot.ReplyMessage(replyToken, "⚠️ 找不到您的帳號，請先完成綁定。");
                                continue;
                            }

                            var role = string.IsNullOrEmpty(user.Role) ? "未設定" : user.Role;
                            bot.ReplyMessage(replyToken, $"🧑 您的角色是：{role}");
                            continue;
                        }


                        var profile = isRock.LineBot.Utility.GetUserInfo(uid, _token);
                        var lineDisplayName = profile.displayName ?? "未命名";


                        // 1. 確保 LineUsers
                        var lineUser = await _db.LineUsers.FirstOrDefaultAsync(x => x.LineUserId == uid);
                        if (lineUser == null)
                        {
                            lineUser = new LineUser
                            {
                                LineUserId = uid,
                                DisplayName = lineDisplayName,
                                CreatedAt = DateTime.Now
                            };
                            _db.LineUsers.Add(lineUser);
                            if (!TrySave(replyToken)) continue;

                        }
                        else
                        {
                            lineUser.DisplayName = lineDisplayName;
                            if (!TrySave(replyToken)) continue;

                        }


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

                        //  統一取出 action
                        kv.TryGetValue("action", out var action);

                        // ====== 待審核清單分頁 ======
                        if (action == "reviewListPage")
                        {
                            int.TryParse(kv.GetValueOrDefault("page"), out var page);
                            if (page <= 0) page = 1;
                            var apps = await _carAppService.GetAll(DateTime.Today, DateTime.Today.AddDays(7), null, User);
                            var bubbleJson = MessageBuilder.BuildPendingListBubble(page, 5, apps);
                            if (bubbleJson != null)
                            {
                                await BotJson.ReplyAsync(replyToken, bubbleJson, _token);
                            }
                            else
                            {
                                bot.ReplyMessage(replyToken, "目前沒有待審核的申請單。");
                            }
                            return Ok();
                        }

                        // ====== 同意申請 → 進入選駕駛流程 ======
                        if (action == "reviewApprove")
                        {
                            int.TryParse(kv.GetValueOrDefault("applyId"), out var applyId);
                            var app = await _db.CarApplications.FirstOrDefaultAsync(a => a.ApplyId == applyId);
                            if (app == null)
                            {
                                bot.ReplyMessage(replyToken, "⚠️ 找不到該申請單。");
                                return Ok();
                            }

                            app.Status = "審核通過(待指派)";
                            if (!TrySave(replyToken)) continue;

                            var selectDriverBubble = MessageBuilder.BuildDriverSelectBubble(applyId, _db);
                            bot.ReplyMessageWithJSON(replyToken, $"[{selectDriverBubble}]");
                            return Ok();
                        }

                        // ====== 拒絕申請 ======
                        if (action == "reviewReject")
                        {
                            int.TryParse(kv.GetValueOrDefault("applyId"), out var applyId);
                            var app = await _db.CarApplications.FirstOrDefaultAsync(a => a.ApplyId == applyId);
                            if (app == null)
                            {
                                bot.ReplyMessage(replyToken, "⚠️ 找不到該申請單");
                                return Ok();
                            }

                            app.Status = "駁回";
                            if (!TrySave(replyToken)) continue;

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

                            var app = await _db.CarApplications.FirstOrDefaultAsync(a => a.ApplyId == applyId);
                            if (app == null)
                            {
                                bot.ReplyMessage(replyToken, "⚠️ 找不到該申請單");
                                return Ok();
                            }

                            var state = _flow.GetOrAdd(uid, _ => new BookingState());
                            state.SelectedDriverId = driverId;
                            state.SelectedDriverName = driverName;

                            bot.ReplyMessage(replyToken, $"✅ 已選擇駕駛：{driverName}");

                            var carBubble = MessageBuilder.BuildCarSelectBubble(applyId, _db);
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

                            var app = await _db.CarApplications.FirstOrDefaultAsync(a => a.ApplyId == applyId);
                            if (app == null)
                            {
                                bot.ReplyMessage(replyToken, "⚠️ 找不到對應申請單");
                                return Ok();
                            }

                            var dispatch = await _db.Dispatches
                                .OrderByDescending(d => d.DispatchId)
                                .FirstOrDefaultAsync(d => d.ApplyId == applyId);

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
                            app.IsLongTrip = (app.SingleDistance ?? 0) > 30;
                            app.Status = "完成審核";
                            if (!TrySave(replyToken)) continue;

                            bot.ReplyMessage(replyToken, $"✅ 已選擇車輛：{plateNo}");

                            var doneBubble = MessageBuilder.BuildDoneBubble(driverState.SelectedDriverName, plateNo);
                            bot.PushMessageWithJSON(uid, $"[{doneBubble}]");

                            if (_applyToApplicant.TryGetValue(app.ApplyId, out var applicantUid))
                            {
                                if (applicantUid != uid) // 避免自己收到兩次
                                    bot.PushMessageWithJSON(applicantUid, $"[{doneBubble}]");
                            }
                            else
                            {
                                // 沒找到申請人 → 至少推給操作者一次
                                bot.PushMessageWithJSON(uid, $"[{doneBubble}]");
                            }

                            // 從資料庫找出對應駕駛的 LineUserId
                            var driverLineId = await (from d in _db.Drivers
                                                join u in _db.Users on d.UserId equals u.UserId
                                                where d.DriverId == app.DriverId && u.LineUserId != null && u.LineUserId != ""
                                                select u.LineUserId).FirstOrDefaultAsync();


                            if (!string.IsNullOrEmpty(driverLineId))
                            {
                                var notice = MessageBuilder.BuildDriverDispatchBubble(app, driverState.SelectedDriverName, plateNo, km, minutes);
                                bot.PushMessageWithJSON(driverLineId, $"[{notice}]");
                            }


                            _flow.TryRemove(uid, out _);
                            return Ok();
                        }
                    }

                    // ================= MESSAGE 事件 =================
                    if (ev.type == "message")
                    {
                        var state = _flow.GetOrAdd(uid, _ => new BookingState());
                        // 全域訊息安全檢查
                        if (!InputValidator.IsValidUserText(msg) || InputValidator.ContainsSqlMeta(msg))
                        {
                            bot.ReplyMessage(replyToken, "輸入格式不正確，請重新輸入。");
                            continue;
                        }
                        // 管理員：查看待審核清單
                        if (msg == "待審核")
                        {
                            // 角色確認
                            var isAdmin = _db.Users.Any(u => u.LineUserId == uid && (u.Role == "Admin" || u.Role == "Manager"));
                            if (!isAdmin)
                            {
                                bot.ReplyMessage(replyToken, "您沒有權限查看待審核清單。");
                                continue;
                            }

                            // 查詢資料
                            var apps = await _carAppService.GetAll(DateTime.Today, DateTime.Today.AddDays(7), null, User);

                            // 第 1 頁（page 預設值）
                            int page = 1;
                            var bubbleJson = MessageBuilder.BuildPendingListBubble(page, 5, apps);

                            if (!string.IsNullOrEmpty(bubbleJson))
                            {
                                await BotJson.ReplyAsync(replyToken, bubbleJson, _token);
                            }
                            else
                            {
                                bot.ReplyMessage(replyToken, "目前沒有待審核的申請單。");
                            }

                            continue;
                        }

                        if (msg == "我的行程")
                        {
                            // 1. 找到目前使用者
                            var user = await _db.Users.FirstOrDefaultAsync(u => u.LineUserId == uid);
                            if (user == null)
                            {
                                bot.ReplyMessage(replyToken, "⚠️ 找不到您的帳號，請先完成綁定。");
                                continue;
                            }

                            var today = DateTime.Today;
                            bool hasResult = false;

                            // ===== A. 申請人身份 =====
                            var applicant = await _db.Applicants.FirstOrDefaultAsync(a => a.UserId == user.UserId);
                            if (applicant != null)
                            {
                                var apps = _db.CarApplications
                                    .Where(a => a.ApplicantId == applicant.ApplicantId &&
                                                a.UseStart.Date == today)
                                    .ToList();

                                if (apps.Any())
                                {
                                    var lines = apps.Select(a =>
                                        $"📝 申請單 {a.ApplyId}\n" +
                                        $"⏰ {a.UseStart:HH:mm} - {a.UseEnd:HH:mm}\n" +
                                        $"🚗 {a.Origin} → {a.Destination}");
                                    var reply = "📌 您今天的申請行程：\n\n" + string.Join("\n\n", lines);
                                    bot.ReplyMessage(replyToken, reply);
                                    hasResult = true;
                                }
                            }

                            // ===== B. 司機身份 =====
                            var driver = await _db.Drivers.FirstOrDefaultAsync(d => d.UserId == user.UserId);
                            if (driver != null)
                            {
                                var dispatches = _db.Dispatches
                                    .Include(d => d.CarApplication)
                                    .Include(d => d.Vehicle) 
                                    .Where(d => d.DriverId == driver.DriverId &&
                                                d.StartTime.HasValue &&
                                                d.StartTime.Value.Date == today)
                                    .ToList();


                                if (dispatches.Any())
                                {
                                    var lines = dispatches.Select(d =>
                                        $"📝 派車單 {d.DispatchId}\n" +
                                        $"⏰ {d.StartTime:HH:mm} - {d.EndTime:HH:mm}\n" +
                                        $"🚗 {d.CarApplication.Origin} → {d.CarApplication.Destination}");
                                    var reply = "📌 您今天的派車任務：\n\n" + string.Join("\n\n", lines);
                                    bot.ReplyMessage(replyToken, reply);
                                    hasResult = true;
                                }

                                if (msg == "開始行程")
                                {
                                    var result = await _dispatchService.StartTripAsync(driver.DriverId, uid);
                                    bot.ReplyMessage(replyToken, result);
                                    continue;
                                }

                                if (msg == "結束行程")
                                {
                                    var result = await _dispatchService.EndTripAsync(driver.DriverId, uid);
                                    bot.ReplyMessage(replyToken, result);
                                    continue;
                                }
                            }

                            // ===== C. 兩者皆非 =====
                            if (!hasResult)
                            {
                                bot.ReplyMessage(replyToken, "📌 今天沒有您的行程或派車任務。");
                            }

                            continue;
                        }
                       

                        // Step 1: 開始預約
                        if (msg.Contains("預約車輛"))
                        {
                            var role = await GetUserRole(uid);
                            if (role == "Driver")
                            {
                                bot.ReplyMessage(replyToken, "⚠️ 您沒有申請派車的權限，請聯絡管理員開通帳號");
                                continue;
                            }

                            _flow[uid] = new BookingState(); // reset
                            bot.ReplyMessageWithJSON(replyToken, MessageBuilder.BuildStep1());
                            continue;
                        }

                        // Step 2: 預約時間
                        if (string.IsNullOrEmpty(state.ReserveTime))
                        {
                            // 即時預約
                            if (msg == "即時預約")
                            {
                                state.ReserveTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm");

                                var arriveMenu = MessageBuilder.BuildDepartureTimeQuickReply("抵達時間", DateTime.Today);
                                await BotJson.ReplyAsync(replyToken, arriveMenu, _token);
                                bot.ReplyMessageWithJSON(replyToken, arriveMenu);
                                continue;
                            }

                            // 預訂時間（QuickReply）
                            if (msg == "預訂時間")
                            {
                                var reserveJson = MessageBuilder.BuildDepartureTimeQuickReply("出發時間", DateTime.Today);
                                await BotJson.ReplyAsync(replyToken, reserveJson, _token);
                                bot.ReplyMessageWithJSON(replyToken, reserveJson);
                                continue;
                            }

                            // 手動輸入出發時間
                            if (msg == "手動輸入")
                            {
                                state.WaitingForManualDeparture = true;
                                bot.ReplyMessage(replyToken, "請輸入出發時間，格式：HH:mm 或 yyyy/MM/dd HH:mm");
                                continue;
                            }

                            // 如果正在等待手動出發時間
                            if (state.WaitingForManualDeparture)
                            {
                                string error;
                                if (TryHandleManualTime(state, msg, "出發時間", out error))
                                {
                                    state.WaitingForManualDeparture = false;

                                    var arriveMenu = MessageBuilder.BuildDepartureTimeQuickReply("抵達時間", DateTime.Today);
                                    await BotJson.ReplyAsync(replyToken, arriveMenu, _token);
                                    bot.ReplyMessageWithJSON(replyToken, arriveMenu);
                                    continue;
                                }
                                else
                                {
                                    bot.ReplyMessage(replyToken, error);
                                    continue;
                                }
                            }

                            // QuickReply 選單點選的時間（不是手動）
                            DateTime depTime;
                            if (DateTime.TryParse(msg, out depTime))
                            {
                                state.ReserveTime = depTime.ToString("yyyy/MM/dd HH:mm");

                                var arriveMenu = MessageBuilder.BuildDepartureTimeQuickReply("抵達時間", DateTime.Today);
                                await BotJson.ReplyAsync(replyToken, arriveMenu, _token);
                                bot.ReplyMessageWithJSON(replyToken, arriveMenu);
                                continue;
                            }
                        }

                        // Step 3: 抵達時間
                        if (!string.IsNullOrEmpty(state.ReserveTime) && string.IsNullOrEmpty(state.ArrivalTime))
                        {
                            // 手動輸入抵達時間
                            if (msg == "手動輸入")
                            {
                                state.WaitingForManualArrival = true;
                                bot.ReplyMessage(replyToken, "請輸入抵達時間，格式：HH:mm 或 yyyy/MM/dd HH:mm");
                                continue;
                            }

                            // 如果正在等待手動抵達時間
                            if (state.WaitingForManualArrival)
                            {
                                string error;
                                if (TryHandleManualTime(state, msg, "抵達時間", out error))
                                {
                                    state.WaitingForManualArrival = false;
                                    bot.ReplyMessage(replyToken, "請輸入用車事由");
                                    continue;
                                }
                                else
                                {
                                    bot.ReplyMessage(replyToken, error);
                                    continue;
                                }
                            }

                            // QuickReply 選單點選的抵達時間
                            DateTime arrTime;
                            if (DateTime.TryParse(msg, out arrTime))
                            {
                                var dep = DateTime.Parse(state.ReserveTime);
                                if (arrTime <= dep.AddMinutes(10))
                                {
                                    bot.ReplyMessage(replyToken, "⚠️ 抵達時間需晚於出發時間 10 分鐘以上");
                                    var reserveJson = MessageBuilder.BuildDepartureTimeQuickReply("抵達時間", DateTime.Today);
                                    await BotJson.ReplyAsync(replyToken, reserveJson, _token);
                                    bot.ReplyMessageWithJSON(replyToken, reserveJson);
                                    continue;
                                }

                                state.ArrivalTime = arrTime.ToString("yyyy/MM/dd HH:mm");
                                bot.ReplyMessage(replyToken, "請輸入用車事由");
                                continue;
                            }
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
                            string reason, err;
                            if (!InputValidator.IsValidReason(msg, out reason, out err))
                            {
                                // 不通過就擋下，不往下個步驟
                                bot.ReplyMessage(replyToken, err + " 請重新輸入（範例：公務出差、會議接送、訪視、搬運…）");
                                continue;
                            }

                            // 通過才寫入並進下一步
                            state.Reason = reason;
                            bot.ReplyMessageWithJSON(replyToken, MessageBuilder.BuildStep3());
                            continue;
                        }

                        // Step 4: 人數
                        if (!string.IsNullOrEmpty(state.Reason) &&
                            !state.PassengerCount.HasValue &&
                            msg.EndsWith("人"))
                        {
                            int pax;
                            if (int.TryParse(msg.Replace("人", ""), out pax))
                            {
                                state.PassengerCount = pax;
                                bot.ReplyMessage(replyToken, "請輸入出發地點");
                            }
                            else
                            {
                                bot.ReplyMessage(replyToken, "⚠️ 人數格式錯誤，請重新輸入（例如：3人）");
                            }
                            continue;
                        }


                        // Step 5: 出發地



                        if (state.PassengerCount.HasValue &&
                            string.IsNullOrEmpty(state.Origin) &&
                            msg != "確認" && msg != "取消")
                        {
                            if (!InputValidator.IsValidLocation(msg))
                            {
                                bot.ReplyMessage(replyToken, "⚠️ 地點格式不正確，請重新輸入（例如：台北市中正區…）");
                                continue;
                            }
                            var result = await ValidateAddressAsync(msg);
                            if (!result.ok)
                            {
                                bot.ReplyMessage(replyToken, result.error);
                                continue;
                            }

                            state.Origin = result.formatted;
                            bot.ReplyMessage(replyToken, "請輸入前往地點");
                            continue;
                        }

                        // Step 6: 目的地



                        if (!string.IsNullOrEmpty(state.Origin) &&
                            string.IsNullOrEmpty(state.Destination) &&
                            msg != "確認" && msg != "取消")
                        {
                            if (!InputValidator.IsValidLocation(msg))
                            {
                                bot.ReplyMessage(replyToken, "⚠️ 地點格式不正確，請重新輸入（例如：台北市中正區…）");
                                continue;
                            }
                            var result = await ValidateAddressAsync(msg);
                            if (!result.ok)
                            {
                                bot.ReplyMessage(replyToken, result.error);
                                continue;
                            }

                            state.Destination = result.formatted;
                            bot.ReplyMessageWithJSON(replyToken, MessageBuilder.BuildStep6());
                            continue;
                        }


                        // Step 6b: 單程/來回
                        if (!string.IsNullOrEmpty(state.Destination) &&
                            string.IsNullOrEmpty(state.TripType) &&
                            (msg == "單程" || msg == "來回"))
                        {
                            state.TripType = (msg == "單程") ? "single" : "round";

                            // 確認卡片
                            var safeReserveTime = SafeText(state.ReserveTime);
                            var safeReason = SafeText(state.Reason);
                            var safePax = state.PassengerCount ?? 1;
                            var safeOrigin = SafeText(state.Origin);
                            var safeDest = SafeText(state.Destination);
                            string confirmBubble = MessageBuilder.BuildConfirmBubble(state);
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
                            var role = await GetUserRole(uid);
                            if (role != "Applicant" && role != "Admin")
                            {
                                bot.ReplyMessage(replyToken, "⚠️ 您沒有建立派車申請的權限");
                                continue;
                            }

                            // === Step 1. 呼叫 Distance API ===
                            double km = 0, minutes = 0;
                            try
                            {
                                var url = $"{_baseUrl}/api/distance?origin={Uri.EscapeDataString(state.Origin ?? "公司")}&destination={Uri.EscapeDataString(state.Destination ?? "")}";
                                var resDist = await _http.GetStringAsync(url);
                                var json = JObject.Parse(resDist);
                                km = json["distanceKm"]?.Value<double>() ?? 0;
                                minutes = json["durationMin"]?.Value<double>() ?? 0;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("⚠️ Distance API 失敗: " + ex.Message);
                            }

                            // === Step 2. 出發/抵達時間 ===
                            var start = DateTime.TryParse(state.ReserveTime, out var tmpStart) ? tmpStart : DateTime.Now;
                            var end = DateTime.TryParse(state.ArrivalTime, out var tmpEnd) ? tmpEnd : start.AddMinutes(60);

                            // === Step 3. 建立申請單 (呼叫 API) ===
                            var appInput = new CarApplication
                            {
                                ApplyReason = state.Reason,
                                Origin = state.Origin ?? "公司",
                                Destination = state.Destination ?? "",
                                UseStart = start,
                                UseEnd = end,
                                PassengerCount = state.PassengerCount ?? 1,
                                TripType = state.TripType ?? "single",
                                SingleDistance = (decimal)km,
                                SingleDuration = MessageBuilder.ToHourMinuteString(minutes),
                                RoundTripDistance = (decimal)(km * 2),
                                RoundTripDuration = MessageBuilder.ToHourMinuteString(minutes * 2),
                            };

                            var jsonBody = JsonConvert.SerializeObject(appInput);
                            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                            var res = await _http.PostAsync($"{_baseUrl}/api/CarApplications/auto-create?lineUserId={uid}", content);
                            if (!res.IsSuccessStatusCode)
                            {
                                var errText = await res.Content.ReadAsStringAsync();
                                Console.WriteLine($"建單 API 失敗: {(int)res.StatusCode} {errText}");
                                bot.ReplyMessage(replyToken, "⚠️ 建單失敗，請稍後再試");
                                continue;
                            }

                            var raw = await res.Content.ReadAsStringAsync();
                            CarApplication created = null;
                            try { created = JsonConvert.DeserializeObject<CarApplication>(raw); }
                            catch (Exception ex)
                            {
                                Console.WriteLine("⚠️ 建單回應解析失敗: " + ex.Message);
                            }
                            if (created == null)
                            {
                                bot.ReplyMessage(replyToken, "⚠️ 建單回應解析失敗");
                                continue;
                            }

                            // === Step 4. 建立派車單 (待指派) ===
                            var resDispatch = await _http.PostAsync($"{_baseUrl}/api/CarApplications/{created.ApplyId}/dispatch", null);
                            if (!resDispatch.IsSuccessStatusCode)
                            {
                                var errText2 = await resDispatch.Content.ReadAsStringAsync();
                                Console.WriteLine($"建立派車單失敗: {(int)resDispatch.StatusCode} {errText2}");
                                bot.ReplyMessage(replyToken, "⚠️ 已建立申請，但派車單建立失敗，請通知管理員協助處理。");
                                continue;
                            }

                            // === Step 5. 推播管理員卡片 ===
                            var profile = isRock.LineBot.Utility.GetUserInfo(uid, _token);
                            var displayName = profile?.displayName ?? "申請人";

                            var forBubble = new CarApplication
                            {
                                ApplyId = created.ApplyId,
                                ApplyReason = state.Reason ?? "—",
                                PassengerCount = state.PassengerCount ?? 1,
                                UseStart = start,
                                UseEnd = end,
                                Destination = state.Destination ?? "—",
                                Applicant = new Applicant { Name = displayName }
                            };

                            var adminFlex = MessageBuilder.BuildAdminFlexBubble(forBubble);

                            var adminIds = _db.Users
                                .Where(u => (u.Role == "Admin" || u.Role == "Manager") && !string.IsNullOrEmpty(u.LineUserId))
                                .Select(u => u.LineUserId)
                                .ToList();

                            foreach (var aid in adminIds)
                                bot.PushMessageWithJSON(aid, $"[{adminFlex}]");

                            // === Step 6. 紀錄申請單對應 ===
                            _applyToApplicant[created.ApplyId] = uid;

                            // === Step 7. 回覆申請人 ===
                            bot.ReplyMessage(replyToken, $"✅ 已送出派車申請（編號 {created.ApplyId}），等待管理員指派。");

                            _flow.TryRemove(uid, out _);
                            continue;
                        }
                        // ================= 管理員審核 =================
                        if (msg.StartsWith("同意申請") || msg.StartsWith("拒絕申請"))
                        {
                            var role = await GetUserRole(uid);
                            if (role != "Admin" && role != "Manager")
                            {
                                bot.ReplyMessage(replyToken, "⚠️ 您沒有審核的權限");
                                continue;
                            }

                            if (!TryParseId(msg, out var applyId))
                            {
                                bot.ReplyMessage(replyToken, "❗ 指令格式錯誤");
                                continue;
                            }

                            var app = await _db.CarApplications
                                               .Include(a => a.Applicant)
                                               .FirstOrDefaultAsync(a => a.ApplyId == applyId);

                            if (app == null)
                            {
                                bot.ReplyMessage(replyToken, "⚠️ 找不到該申請單");
                                continue;
                            }

                            if (msg.StartsWith("同意申請"))
                            {
                                // 顯示「選擇駕駛人」卡片
                                var selectDriverBubble = MessageBuilder.BuildDriverSelectBubble(applyId, _db);
                                bot.ReplyMessageWithJSON(replyToken, $"[{selectDriverBubble}]");
                                continue;
                            }

                            if (msg.StartsWith("拒絕申請"))
                            {
                                app.Status = "已拒絕";
                                if (!TrySave(replyToken)) continue;

                                if (_applyToApplicant.TryGetValue(applyId, out var applicantUid))
                                {
                                    bot.PushMessage(applicantUid,
                                        $"❌ 您的派車申請已被拒絕\n事由：{app.ApplyReason}\n地點：{app.Destination}");
                                }

                                bot.ReplyMessage(replyToken, "✅ 已拒絕該申請。");
                                continue;
                            }
                        }
                    }


                   

                }

                catch (Exception ex)
                {
                    try
                    {
                        // 盡量回覆使用者「不要卡死」
                        var token = (string?)ev?.replyToken ?? "";
                        if (!string.IsNullOrEmpty(token))
                            bot.ReplyMessage(token, "⚠️ 系統忙線中，請稍後再試");
                    }
                    catch { /* 忽略回覆失敗 */ }

                    Console.WriteLine($"[ERROR] Unhandled in event loop (uid={ev?.source?.userId}): {ex}");
                    // 繼續處理下一筆事件
                    continue;
                }
            }

            return Ok();
        }

        #endregion




        #region 檢查當下使用者角色
        // 共用方法：檢查角色
        private async Task<string> GetUserRole(string lineUserId)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.LineUserId == lineUserId);
            return user?.Role ?? "";
        }
        #endregion

        #region 地址轉換與驗證
        // ====== 工具方法：驗證地址（限定台灣） ======
        private async Task<(bool ok, string formatted, double lat, double lng, string error)>
            ValidateAddressAsync(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return (false, "", 0, 0, "⚠️ 請輸入地址");

            try
            {
                var apiKey = _config["GoogleMaps:ApiKey"]; // 需要 IConfiguration
                                                           // 用 components=country:tw 強制限制在台灣，language 讓結果顯示中文
                var url =
                    $"https://maps.googleapis.com/maps/api/geocode/json" +
                    $"?address={Uri.EscapeDataString(input)}" +
                    $"&components=country:tw" +
                    $"&language=zh-TW" +
                    $"&key={apiKey}";

                using var client = new HttpClient();
                var res = await client.GetStringAsync(url);
                var geo = JObject.Parse(res);

                if (geo["status"]?.ToString() != "OK")
                    return (false, "", 0, 0, "⚠️ 找不到此地址，請重新輸入（目前僅支援台灣境內）");

                var result0 = geo["results"]?[0];
                var components = result0?["address_components"] as JArray;

                bool isTW = components != null && components.Any(c =>
                {
                    var types = c["types"] as JArray;
                    if (types == null) return false;
                    bool isCountry = types.Any(t => string.Equals(t?.ToString(), "country", StringComparison.OrdinalIgnoreCase));
                    var shortName = c["short_name"]?.ToString();
                    var longName = c["long_name"]?.ToString() ?? "";
                    return isCountry && (shortName == "TW" || longName.Contains("台灣") || longName.Contains("臺灣") || longName.Contains("Taiwan"));
                });

                if (!isTW)
                    return (false, "", 0, 0, "⚠️ 目前僅支援台灣境內地址，請重新輸入台灣地址");
               
                var formatted = result0?["formatted_address"]?.ToString() ?? input;
                var location = result0?["geometry"]?["location"];
                double lat = location?["lat"]?.Value<double>() ?? 0;
                double lng = location?["lng"]?.Value<double>() ?? 0;
                
                // 額外保險：formatted_address 必須含「台灣/臺灣/Taiwan」
                if (!(formatted.Contains("台灣") || formatted.Contains("臺灣") || formatted.Contains("Taiwan")))
                    return (false, "", 0, 0, "⚠️ 目前僅支援台灣境內地址，請重新輸入台灣地址");

                return (true, formatted, lat, lng, "");
            }
            catch (Exception ex)
            {
                return (false, "", 0, 0, "⚠️ 驗證地址失敗：" + ex.Message);
            }
        }

        #endregion

       
        

       

        

        #region 轉換工具
        // 解析申請單 ID
        private static bool TryParseId(string msg, out int id)
        {
            id = 0;
            var parts = msg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 && int.TryParse(parts[^1], out id);
        }
        

        // 轉為安全的字串（避免特殊字元導致 JSON 格式錯誤）
        private static string SafeText(string? raw, string fallback = "未知")
        {
            return string.IsNullOrWhiteSpace(raw) ? fallback : raw;
        }

        #endregion

        
        // ====== 共用方法：嘗試儲存資料庫 ======
        private bool TrySave(string replyToken, string userMsg = "⚠️ 資料儲存失敗，請稍後再試")
        {
            try { _db.SaveChanges(); return true; }
            catch (DbUpdateException dbex)
            {
                Console.WriteLine("[DB] Update failure: " + dbex.Message);
                _bot.ReplyMessage(replyToken, userMsg);
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[DB] Unknown error: " + ex.Message);
                _bot.ReplyMessage(replyToken, "⚠️ 系統發生錯誤，請稍後再試");
                return false;
            }
        }
        // ====== 共用方法：嘗試解析使用者手動輸入的時間 ======
        private bool TryHandleManualTime(BookingState state, string msg, string type, out string error)
        {
            error = null;
            DateTime parsed;

            if (!DateTime.TryParse(msg, out parsed))
            {
                error = $"⚠️ {type}格式錯誤，請重新輸入，例如 09:30 或 2025/09/26 14:00";
                return false;
            }

            if (type == "抵達時間")
            {
                var dep = DateTime.Parse(state.ReserveTime);
                if (parsed <= dep.AddMinutes(10))
                {
                    error = "⚠️ 抵達時間需晚於出發時間 10 分鐘以上";
                    return false;
                }
                state.ArrivalTime = parsed.ToString("yyyy/MM/dd HH:mm");
            }
            else
            {
                state.ReserveTime = parsed.ToString("yyyy/MM/dd HH:mm");
            }
            return true;
        }

    }




}
