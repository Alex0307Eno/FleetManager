using Cars.Data;
using Cars.Models;
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
        public static string? BuildPendingListBubble(int page, int pageSize, string adminDept, ApplicationDbContext db)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 5;

            // 只取同部門 + 待審核
            var q = db.CarApplications
                .Include(a => a.Applicant)
                .Where(a => a.Status == "待審核" && a.Applicant.Dept == adminDept)
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
    }
}


