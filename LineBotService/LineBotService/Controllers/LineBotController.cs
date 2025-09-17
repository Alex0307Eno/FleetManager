    using Cars.Data;
    using Cars.Models;
    using isRock.LineBot;
    using Microsoft.AspNetCore.Mvc;

    namespace LineBotDemo.Controllers
    {
        [Route("api/[controller]")]
        [ApiController]
        public class LineBotController : ControllerBase
        {
            private readonly string channelAccessToken;
            private readonly ApplicationDbContext _db;

            private const string adminUserId = "UxxxxAdmin";   // ⚠️ 改成實際管理員 userId
            private const string driverUserId = "UxxxxDriver"; // ⚠️ 改成實際駕駛 userId

            private static string Reason = "";
            private static string PassengerCount = "";
            private static string Destination = "";

            public LineBotController(IConfiguration config, ApplicationDbContext db)
            {
                channelAccessToken = config["LineBot:ChannelAccessToken"];
                _db = db;
            }

            [HttpPost]
            public async Task<IActionResult> Post()
            {
                string body;
                using (var reader = new StreamReader(Request.Body))
                {
                    body = await reader.ReadToEndAsync();
                }

                var bot = new Bot(channelAccessToken);
                var events = Utility.Parsing(body);

                foreach (var ev in events.events)
                {
                    if (ev.type != "message") continue;

                    var replyToken = ev.replyToken;
                    var userMessage = ev.message.text;
                    var userId = ev.source.userId; // 申請人 LINE userId

                    // === Step 1: 預約流程 ===
                    if (userMessage.Contains("預約車輛"))
                    {
                        Reason = PassengerCount = Destination = "";
                        bot.ReplyMessage(replyToken, "請輸入用車事由");
                    }
                    // Step 2: 事由
                    else if (Reason == "" && userMessage != "確認")
                    {
                        Reason = userMessage;
                        bot.ReplyMessage(replyToken, "請輸入乘客人數");
                    }
                    // Step 3: 人數
                    else if (PassengerCount == "" && int.TryParse(userMessage.Replace("人", ""), out _))
                    {
                        PassengerCount = userMessage;
                        bot.ReplyMessage(replyToken, "請輸入前往地點");
                    }
                    // Step 4: 地點
                    else if (Destination == "")
                    {
                        Destination = userMessage;

                        // === 綁定或新增申請人 ===
                        var lineUser = _db.LineUsers.FirstOrDefault(x => x.LineUserId == userId);
                        if (lineUser == null)
                        {
                            lineUser = new LineUser
                            {
                                LineUserId = userId,
                                DisplayName = "未知使用者",
                                Role = "申請人",
                                CreatedAt = DateTime.Now
                            };
                            _db.LineUsers.Add(lineUser);
                            _db.SaveChanges();
                        }

                        // === 建立申請單 ===
                        var app = new CarApplication
                        {
                            ApplyFor = lineUser.DisplayName,
                            ApplicantId = lineUser.RelatedId, // 可選（如果已經綁定 Applicant）
                            ApplyReason = Reason,
                            PassengerCount = int.Parse(PassengerCount.Replace("人", "")),
                            Origin = "森林規劃科",
                            Destination = Destination,
                            UseStart = DateTime.Now,
                            UseEnd = DateTime.Now.AddHours(2),
                            Status = "待審核"
                        };
                        _db.CarApplications.Add(app);
                        _db.SaveChanges();

                        // === 通知管理員 ===
                        string adminFlex = GetAdminFlexJson(app);
                        bot.PushMessageWithJSON(adminUserId, adminFlex);

                        bot.ReplyMessage(replyToken, "✅ 已送出申請，等待管理員審核");
                    }
                    // === Step 5: 管理員審核 ===
                    else if (userMessage == "同意申請")
                    {
                        string driverFlex = GetDriverSelectFlexJson();
                        bot.ReplyMessageWithJSON(replyToken, driverFlex);
                    }
                    else if (userMessage == "拒絕申請")
                    {
                        var app = _db.CarApplications
                                     .OrderByDescending(a => a.ApplyId)
                                     .FirstOrDefault(a => a.Status == "待審核");

                        if (app != null)
                        {
                            app.Status = "已拒絕";
                            _db.SaveChanges();

                            // 找出申請人
                            var lineUser = _db.LineUsers.FirstOrDefault(u => u.RelatedId == app.ApplicantId);
                            if (lineUser != null)
                            {
                                bot.PushMessage(lineUser.LineUserId,
                                    $"❌ 您的派車申請已被拒絕\n" +
                                    $"事由：{app.ApplyReason}\n" +
                                    $"地點：{app.Destination}");
                            }

                            bot.ReplyMessage(replyToken, "✅ 已拒絕該派車申請");
                        }
                    }
                    // === Step 6: 指派駕駛 ===
                    else if (userMessage.StartsWith("指派駕駛:"))
                    {
                        string driverName = userMessage.Replace("指派駕駛:", "");
                        string doneFlex = GetDoneFlexJson(driverName, "0000");

                        bot.ReplyMessageWithJSON(replyToken, doneFlex);

                        // 找最新的申請人
                        var app = _db.CarApplications
                                     .OrderByDescending(a => a.ApplyId)
                                     .FirstOrDefault(a => a.Status == "待審核");

                        if (app != null)
                        {
                            app.Status = "已派車";
                            _db.SaveChanges();

                            // 找申請人
                            var lineUser = _db.LineUsers.FirstOrDefault(u => u.RelatedId == app.ApplicantId);
                            if (lineUser != null)
                            {
                                bot.PushMessageWithJSON(lineUser.LineUserId, doneFlex);
                            }

                            // 通知駕駛
                            bot.PushMessageWithJSON(driverUserId, doneFlex);
                        }
                    }
                    else
                    {
                        bot.ReplyMessage(replyToken, $"你剛剛說：{userMessage}");
                    }
                }

                return Ok();
            }

            // 管理員審核卡片
            private string GetAdminFlexJson(CarApplication app) => $@"
            {{
              ""type"": ""flex"",
              ""altText"": ""派車申請審核"",
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
                    {{ ""type"": ""text"", ""text"": ""■ 前往地點：{app.Destination}"" }}
                  ]
                }},
                ""footer"": {{
                  ""type"": ""box"",
                  ""layout"": ""horizontal"",
                  ""contents"": [
                    {{ ""type"": ""button"", ""style"": ""secondary"", ""action"": {{ ""type"": ""message"", ""label"": ""拒絕"", ""text"": ""拒絕申請"" }} }},
                    {{ ""type"": ""button"", ""style"": ""primary"", ""action"": {{ ""type"": ""message"", ""label"": ""同意"", ""text"": ""同意申請"" }} }}
                  ]
                }}
              }}
            }}";

            // 選駕駛人
            private string GetDriverSelectFlexJson() => @"
            {
              ""type"": ""flex"",
              ""altText"": ""選擇駕駛人"",
              ""contents"": {
                ""type"": ""bubble"",
                ""body"": {
                  ""type"": ""box"",
                  ""layout"": ""vertical"",
                  ""contents"": [
                    { ""type"": ""text"", ""text"": ""請選擇駕駛人"", ""weight"": ""bold"", ""size"": ""lg"" },
                    { ""type"": ""button"", ""action"": { ""type"": ""message"", ""label"": ""王ＯＯ"", ""text"": ""指派駕駛:王ＯＯ"" }},
                    { ""type"": ""button"", ""action"": { ""type"": ""message"", ""label"": ""吳ＸＸ"", ""text"": ""指派駕駛:吳ＸＸ"" }}
                  ]
                }
              }
            }";

            // 完成通知
            private string GetDoneFlexJson(string driver, string carNo) => $@"
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
                    {{ ""type"": ""text"", ""text"": ""■ 駕駛人：{driver}"" }},
                    {{ ""type"": ""text"", ""text"": ""■ 使用車輛：{carNo}"" }}
                  ]
                }}
              }}
            }}";
        }
    }
