using Cars.Data;
using Cars.Models;
using Cars.Services;
using Microsoft.EntityFrameworkCore;

namespace LineBotService.Services
{
    public static class MessageBuilder
    {
        // Step 1: 即時預約 or 預訂時間
        public static string BuildStep1()
        {
            return @"
            [
              {
                ""type"": ""template"",
                ""altText"": ""請選擇預約方式"",
                ""template"": {
                  ""type"": ""confirm"",
                  ""text"": ""請選擇預約的時間"",
                  ""actions"": [
                    { ""type"": ""message"", ""label"": ""即時預約"", ""text"": ""即時預約"" },
                    { ""type"": ""message"", ""label"": ""預訂時間"", ""text"": ""預訂時間"" }
                  ]
                }
              }
            ]";
        }

        // Step 3: 乘客人數 1~4人
        public static string BuildStep3()
        {
            return @"
            [
              {
                ""type"": ""template"",
                ""altText"": ""請選擇乘客人數"",
                ""template"": {
                  ""type"": ""buttons"",
                  ""title"": ""乘客人數"",
                  ""text"": ""請選擇乘客人數"",
                  ""actions"": [
                    { ""type"": ""message"", ""label"": ""1人"", ""text"": ""1人"" },
                    { ""type"": ""message"", ""label"": ""2人"", ""text"": ""2人"" },
                    { ""type"": ""message"", ""label"": ""3人"", ""text"": ""3人"" },
                    { ""type"": ""message"", ""label"": ""4人"", ""text"": ""4人"" }
                  ]
                }
              }
            ]";
        }

        // Step 6: 行程類型 單程 or 來回
        public static string BuildStep6()
        {
            return @"
            [
              {
                ""type"": ""template"",
                ""altText"": ""請選擇行程類型"",
                ""template"": {
                  ""type"": ""confirm"",
                  ""text"": ""請選擇行程類型"",
                  ""actions"": [
                    { ""type"": ""message"", ""label"": ""單程"", ""text"": ""單程"" },
                    { ""type"": ""message"", ""label"": ""來回"", ""text"": ""來回"" }
                  ]
                }
              }
            ]";
        }
        // 確認申請資訊
        public static string BuildConfirmBubble(BookingState state)
        {
            return $@"
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
                    {{ ""type"": ""text"", ""text"": ""■ 乘客人數：{state.PassengerCount ?? 1}"" }},
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
        }

        // 動態時間 QuickReply
        public static string BuildDepartureTimeQuickReply(string title, DateTime baseDay, DateTime? minTime = null)
        {
            var now = DateTime.Now.AddMinutes(5);
            var floor = minTime ?? now;
            var day = baseDay.Date;

            var slots = Enumerable.Range(8, 10)
                .Select(h => new DateTime(day.Year, day.Month, day.Day, h, 0, 0))
                .Where(t => t >= floor)
                .ToList();

            if (!slots.Any())
            {
                day = day.AddDays(1);
                slots = Enumerable.Range(8, 10)
                    .Select(h => new DateTime(day.Year, day.Month, day.Day, h, 0, 0))
                    .ToList();
            }

            var columns = new List<string>();
            var groups = slots.Select((t, i) => new { t, i }).GroupBy(x => x.i / 3).ToList();
            int page = 1;

            foreach (var g in groups)
            {
                var actions = g.Select(x =>
                    $@"{{ ""type"": ""message"", ""label"": ""{x.t:HH:mm}"", ""text"": ""{x.t:yyyy/MM/dd HH:mm}"" }}").ToList();

                while (actions.Count < 3)
                    actions.Add(@"{ ""type"": ""message"", ""label"": ""—"", ""text"": ""—"" }");

                columns.Add($@"
                {{
                  ""title"": ""{title} ({page})"",
                  ""text"": ""請選擇時間"",
                  ""actions"": [ {string.Join(",", actions)} ]
                }}");
                page++;
            }

            columns.Add(@"
            {
              ""title"": ""其他選項"",
              ""text"": ""請選擇"",
              ""actions"": [
                { ""type"": ""message"", ""label"": ""手動輸入"", ""text"": ""手動輸入"" },
                { ""type"": ""message"", ""label"": ""取消"", ""text"": ""取消"" },
                { ""type"": ""message"", ""label"": ""返回主選單"", ""text"": ""返回主選單"" }
              ]
            }");

            var json = $@"
            [ {{
              ""type"": ""template"",
              ""altText"": ""請選擇{title}"",
              ""template"": {{
                ""type"": ""carousel"",
                ""columns"": [ {string.Join(",", columns)} ]
              }}
            }} ]";

            return json;
        }
        //管理員審核清單卡片
        public static string? BuildPendingListBubble(int page, int pageSize, List<CarApplicationDto> apps)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 5;

            // 取待審核
            var q = apps
                .Where(a => string.Equals(a.Status, "待審核", StringComparison.OrdinalIgnoreCase))
                .OrderBy(a => a.UseStart)
                .ToList();

            var total = q.Count();
            if (total == 0) return null;

            var items = q.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            // 每筆申請格式
            var cardContents = string.Join(",\n", items.Select(a => $@"
    {{
      ""type"": ""box"",
      ""layout"": ""vertical"",
      ""spacing"": ""xs"",
      ""margin"": ""md"",
      ""paddingAll"": ""10px"",
      ""borderWidth"": ""1px"",
      ""borderColor"": ""#d1d5db"",
      ""cornerRadius"": ""md"",
      ""contents"": [
        {{ ""type"": ""text"", ""text"": ""申請單 #{a.ApplyId}"", ""weight"": ""bold"", ""size"": ""md"", ""color"": ""#0f172a"" }},
        {{ ""type"": ""text"", ""text"": ""申請人：{a.ApplicantName ?? "—"} ({a.ApplicantDept ?? "—"})"", ""size"": ""sm"", ""color"": ""#334155"" }},
        {{ ""type"": ""text"", ""text"": ""用車時間：{a.UseStart:MM/dd HH:mm} - {a.UseEnd:HH:mm}"", ""size"": ""sm"", ""color"": ""#334155"", ""wrap"": true }},
        {{ ""type"": ""text"", ""text"": ""路線：{(a.Origin ?? "公司")} → {a.Destination ?? "未填寫"}"", ""size"": ""sm"", ""color"": ""#475569"", ""wrap"": true }},
        {{ ""type"": ""text"", ""text"": ""乘客：{a.PassengerCount ?? 1} 人｜行程：{(a.TripType == "round" ? "來回" : "單程")}"", ""size"": ""sm"", ""color"": ""#475569"" }},
        {{ ""type"": ""text"", ""text"": ""事由：{a.ApplyReason ?? "—"}"", ""size"": ""sm"", ""color"": ""#64748b"", ""wrap"": true }},
        {{ ""type"": ""separator"", ""margin"": ""sm"" }},
        {{
          ""type"": ""box"",
          ""layout"": ""horizontal"",
          ""spacing"": ""md"",
          ""margin"": ""sm"",
          ""contents"": [
            {{
              ""type"": ""button"",
              ""style"": ""secondary"",
              ""color"": ""#ef4444"",
              ""height"": ""sm"",
              ""action"": {{
                ""type"": ""postback"",
                ""label"": ""❌ 駁回"",
                ""data"": ""action=reviewReject&applyId={a.ApplyId}""
              }}
            }},
            {{
              ""type"": ""button"",
              ""style"": ""primary"",
              ""color"": ""#22c55e"",
              ""height"": ""sm"",
              ""action"": {{
                ""type"": ""postback"",
                ""label"": ""✅ 同意"",
                ""data"": ""action=reviewApprove&applyId={a.ApplyId}""
              }}
            }}
          ]
        }}
      ]
    }}"));

            // 頁尾
            var totalPages = (int)Math.Ceiling(total / (double)pageSize);
            var hasPrev = page > 1;
            var hasNext = page < totalPages;

            var footerButtons = new List<string>();

            if (hasPrev)
            {
                footerButtons.Add(@$"{{
          ""type"": ""button"",
          ""style"": ""secondary"",
          ""height"": ""sm"",
          ""action"": {{
            ""type"": ""postback"",
            ""label"": ""⬅️ 上一頁"",
            ""data"": ""action=reviewListPage&page={page - 1}""
          }}
        }}");
            }

            footerButtons.Add(@$"{{
        ""type"": ""text"",
        ""text"": ""第 {page}/{totalPages} 頁"",
        ""align"": ""center"",
        ""size"": ""sm"",
        ""color"": ""#64748b""
    }}");

            if (hasNext)
            {
                footerButtons.Add(@$"{{
          ""type"": ""button"",
          ""style"": ""secondary"",
          ""height"": ""sm"",
          ""action"": {{
            ""type"": ""postback"",
            ""label"": ""下一頁 ➡️"",
            ""data"": ""action=reviewListPage&page={page + 1}""
          }}
        }}");
            }

            var footer = string.Join(",", footerButtons);

            // 組合 Flex
            var bubble = $@"
    {{
      ""type"": ""flex"",
      ""altText"": ""待審核派車清單"",
      ""contents"": {{
        ""type"": ""bubble"",
        ""size"": ""mega"",
        ""header"": {{
          ""type"": ""box"",
          ""layout"": ""horizontal"",
          ""contents"": [
            {{ ""type"": ""text"", ""text"": ""🚗 派車申請審核清單"", ""weight"": ""bold"", ""size"": ""md"", ""color"": ""#0f172a"" }}
          ]
        }},
        ""body"": {{
          ""type"": ""box"",
          ""layout"": ""vertical"",
          ""spacing"": ""md"",
          ""contents"": [
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

        #region 通知

        //申請人通知卡片
        public static string BuildAdminFlexBubble(CarApplication app) =>
            $@"
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
                {{ ""type"": ""text"", ""text"": ""■ 申請人：{app.Applicant?.Name}"" }},        
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
        public static string BuildDriverSelectBubble(int applyId, ApplicationDbContext db)
        {
            var app = db.CarApplications.FirstOrDefault(a => a.ApplyId == applyId);
            if (app == null)
                return @"{ ""type"": ""text"", ""text"": ""⚠️ 找不到該申請單"" }";

            var useStart = app.UseStart;
            var useEnd = app.UseEnd;

            var drivers = db.Drivers
                .Where(d => !d.IsAgent &&
                    !db.CarApplications.Any(ca =>
                        ca.DriverId == d.DriverId &&
                        ca.ApplyId != applyId && // 排除自己
                        ca.UseStart < useEnd &&  // 交疊判斷
                        ca.UseEnd > useStart
                    )
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
        public static string BuildCarSelectBubble(int applyId, ApplicationDbContext db)
        {
            var app = db.CarApplications.FirstOrDefault(a => a.ApplyId == applyId);
            if (app == null)
                return @"[{ ""type"": ""text"", ""text"": ""⚠️ 找不到該申請單"" }]";

            var useStart = app.UseStart;
            var useEnd = app.UseEnd;

            var cars = db.Vehicles
                .Where(v => v.Status == "可用" &&
                    !db.CarApplications.Any(ca =>
                        ca.VehicleId == v.VehicleId &&
                        ca.ApplyId != applyId && // 排除自己
                        ca.UseStart < useEnd &&
                        ca.UseEnd > useStart
                    )
                )
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
        public static string BuildDoneBubble(string driverName, string carNo) => $@"
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
        public static string BuildDriverDispatchBubble(CarApplication app, string driverName, string carNo, double km, double minutes)
        {
            // 根據行程類型決定顯示距離/時間
            bool isRound = app.TripType == "round";

            double showKm = isRound ? km * 2 : km;
            double showMinutes = isRound ? minutes * 2 : minutes;

            string distanceText = $"■ 距離：約 {showKm:F1} 公里";
            string durationText = $"■ 車程：約 {ToHourMinuteString(showMinutes)}";
            var safeApplyFor = SafeJson(app.ApplyFor);
            var safeOrigin = SafeJson(app.Origin);
            var safeDest = SafeJson(app.Destination);
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
       
        #endregion
        // 轉為安全的字串（避免特殊字元導致 JSON 格式錯誤）
        private static string SafeJson(string? raw)
        {
            // 轉為安全 JSON 字面值再去除最外層引號 → 適合放到 text 欄位
            var json = Newtonsoft.Json.JsonConvert.ToString(raw ?? "");
            return json.Length >= 2 ? json.Substring(1, json.Length - 2) : "";
        }
        // 分鐘轉換成「X 小時 Y 分鐘」格式
        public static string ToHourMinuteString(double minutes)
        {
            int totalMinutes = (int)Math.Round(minutes);
            int hours = totalMinutes / 60;
            int mins = totalMinutes % 60;

            if (hours > 0)
                return $"{hours} 小時 {mins} 分鐘";
            else
                return $"{mins} 分鐘";
        }

    }
}


