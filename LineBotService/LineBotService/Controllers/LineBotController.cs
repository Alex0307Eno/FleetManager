using Cars.Data;
using Cars.Models;
using DocumentFormat.OpenXml.ExtendedProperties;
using isRock.LIFF;
using isRock.LineBot;
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
        private readonly string _baseUrl;


        public LineBotController(IConfiguration config, ApplicationDbContext db)
        {
            _token = config["LineBot:ChannelAccessToken"];
            _baseUrl = config["AppBaseUrl"];
            _db = db;
        }

        // ===== 使用者流程暫存（依 userId 分別保存） =====
        private class BookingState
        {
            public string? ReserveTime { get; set; }
            public string? Reason { get; set; }
            public string? PassengerCount { get; set; }
            public string? Origin { get; set; }

            public string? Destination { get; set; }

            // 給管理員指派流程用
            public int? SelectedDriverId { get; set; }
            public string? SelectedDriverName { get; set; }
        }
        private static readonly ConcurrentDictionary<string, BookingState> _flow = new();

        // 把「申請單 ApplyId 對應 申請人 LINE userId」暫存起來，方便審核後通知申請人
        private static readonly ConcurrentDictionary<int, string> _applyToApplicant = new();

        // === QuickReply（一定要是 JSON 陣列） ===
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

                    // ========== 指派駕駛 ==========
                    if (kv.TryGetValue("action", out var action) && action == "assignDriver")
                    {
                        int.TryParse(kv.GetValueOrDefault("applyId"), out var applyId);
                        int.TryParse(kv.GetValueOrDefault("driverId"), out var driverId);
                        var driverName = kv.GetValueOrDefault("driverName");

                        var app = _db.CarApplications.FirstOrDefault(a => a.ApplyId == applyId);
                        if (app == null)
                        {
                            bot.ReplyMessage(replyToken, "⚠️ 找不到該申請單");
                            continue;
                        }

                        var state = _flow.GetOrAdd(uid, _ => new BookingState());
                        state.SelectedDriverId = driverId;
                        state.SelectedDriverName = driverName;
                        // 先回覆「已選駕駛」
                        bot.ReplyMessage(replyToken, $"✅ 已選擇駕駛：{driverName}");
                        // 再推送選車卡片
                        var carBubble = BuildCarSelectBubble(applyId, _db);
                        bot.PushMessageWithJSON(uid, $"[{carBubble}]");
                        continue;
                    }

                    // ========== 指派車輛 ==========
                    if (kv.TryGetValue("action", out action) && action == "assignVehicle")
                    {
                        int.TryParse(kv.GetValueOrDefault("applyId"), out var applyId);
                        int.TryParse(kv.GetValueOrDefault("vehicleId"), out var vehicleId);
                        var plateNo = kv.GetValueOrDefault("plateNo");

                        if (!_flow.TryGetValue(uid, out var driverState))
                        {
                            bot.ReplyMessage(replyToken, "⚠️ 找不到駕駛資訊，請重新操作");
                            continue;
                        }

                        var app = _db.CarApplications.FirstOrDefault(a => a.ApplyId == applyId);
                        if (app == null)
                        {
                            bot.ReplyMessage(replyToken, "⚠️ 找不到對應申請單");
                            continue;
                        }
                        // 找出那張「待指派」的派車單
                        var dispatch = _db.Dispatches
                            .OrderByDescending(d => d.DispatchId)
                            .FirstOrDefault(d => d.ApplyId == applyId);

                        if (dispatch == null)
                        {
                            bot.ReplyMessage(replyToken, "⚠️ 找不到對應的派車單");
                            return Ok();
                        }


                        // 更新派車單
                        dispatch.DriverId = driverState.SelectedDriverId ?? 0;
                        dispatch.VehicleId = vehicleId;
                        dispatch.DispatchStatus = "已派車";
                        dispatch.StartTime = DateTime.Now;
                        dispatch.EndTime = app.UseEnd;

                        // === 呼叫 Distance API 算距離 ===
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

                        // 同步更新 CarApplication
                        app.DriverId = driverState.SelectedDriverId;   // 選到的駕駛
                        app.VehicleId = vehicleId;                     // 選到的車輛
                        app.isLongTrip = (app.SingleDistance ?? 0) > 30;
                        app.Status = "完成審核";
                        _db.SaveChanges();
                        // 先回覆「已選車輛」
                        bot.ReplyMessage(replyToken, $"✅ 已選擇車輛：{plateNo}");

                        // 再推送完成審核卡片
                        var doneBubble = BuildDoneBubble(driverState.SelectedDriverName, plateNo);
                        bot.PushMessageWithJSON(uid, $"[{doneBubble}]");

                        if (_applyToApplicant.TryGetValue(app.ApplyId, out var applicantUid))
                            bot.PushMessageWithJSON(applicantUid, $"[{doneBubble}]");

                        // 通知駕駛
                        string driverLineId = "Uc91f19345d05c8ff500d4c02ef71e913";
                        if (!string.IsNullOrEmpty(driverLineId))
                        {
                            var notice = BuildDriverDispatchBubble(app, driverState.SelectedDriverName, plateNo, km, minutes);
                            bot.PushMessageWithJSON(driverLineId, $"[{notice}]");
                        }

                        _flow.TryRemove(uid, out _); // 清掉暫存
                        continue;
                    }
                }

                // ================= MESSAGE 事件 =================
                if (ev.type == "message")
                {
                    var msg = (ev.message.text ?? "").Trim();
                    var state = _flow.GetOrAdd(uid, _ => new BookingState());

                    // Step 1: 開始預約
                    if (msg.Contains("預約車輛"))
                    {
                        _flow[uid] = new BookingState(); // reset
                        bot.ReplyMessageWithJSON(replyToken, Step1JsonArray);
                        continue;
                    }

                    // Step 2: 預約時間
                    if (string.IsNullOrEmpty(state.ReserveTime) && (msg == "即時預約" || msg == "預訂時間"))
                    {
                        state.ReserveTime = msg;
                        bot.ReplyMessage(replyToken, "請輸入用車事由");
                        continue;
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
                        state.Origin = msg;
                        bot.ReplyMessage(replyToken, "請輸入前往地點");
                        continue;
                    }

                    // Step 6: 目的地
                    if (!string.IsNullOrEmpty(state.Origin) &&
                        string.IsNullOrEmpty(state.Destination) &&
                        msg != "確認" && msg != "取消")
                    {
                        state.Destination = msg;

                        // 確認卡片
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
                    { // 先呼叫 Distance API 算距離與時間
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


                        // 建立申請單 (先存 DB)
                        var start = DateTime.Now;
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
                            UseEnd = start.AddMinutes(minutes),
                            ApplicantId = 1,
                            Status = "待審核",

                            // 這邊直接存距離 & 時間
                            SingleDistance = (decimal)km,
                            SingleDuration = $"{minutes:F0} 分鐘",
                            RoundTripDistance = (decimal)(km * 2),
                            RoundTripDuration = $"{minutes * 2:F0} 分鐘",
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
                        var adminIds = _db.LineUsers?.Where(x => x.Role == "Admin").Select(x => x.LineUserId).ToList() ?? new List<string>();
                        var adminFlex = BuildAdminFlexBubble(app);
                        foreach (var aid in adminIds)
                            bot.PushMessageWithJSON(aid, $"[{adminFlex}]");

                        bot.ReplyMessage(replyToken, "✅ 已送出派車申請，等待審核。");
                        _flow.TryRemove(uid, out _);
                        continue;
                    }
                    // ================= 管理員審核 =================
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

                    // 其它訊息：回聲
                    bot.ReplyMessage(replyToken, $"你剛剛說：{msg}");
                }
            }

            return Ok();
        }

        // ====== 產 JSON（都是「單一 bubble」，外層呼叫時會包成 [ ... ]）======
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
            var drivers = db.Drivers
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
            var cars = db.Vehicles
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
        
        // 駕駛人—派車通知
        private static string BuildDriverDispatchBubble(CarApplication app, string driverName, string carNo, double km, double minutes) => $@"
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
        {{ ""type"": ""text"", ""text"": ""■ 距離：約 {km:F1} 公里"" }},
        {{ ""type"": ""text"", ""text"": ""■ 車程：約 {minutes:F0} 分鐘"" }},
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

        private static bool TryParseId(string msg, out int id)
        {
            id = 0;
            var parts = msg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 && int.TryParse(parts[^1], out id);
        }
    }
}
