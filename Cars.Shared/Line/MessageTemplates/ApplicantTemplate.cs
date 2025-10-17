using Cars.Shared.Dtos.Line;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cars.Shared.Line
{
    public static class ApplicantTemplate
    {
        // Step 1: 即時預約 or 預訂時間
        public static string BuildStep1()
        {
            var flex = new
            {
                type = "flex",
                altText = "請選擇預約方式",
                contents = new
                {
                    type = "bubble",
                    body = new
                    {
                        type = "box",
                        layout = "vertical",
                        contents = new object[]
                        {
                    new {
                        type = "box",
                        layout = "vertical",
                        contents = new object[] {
                            new { type = "text", text = "請選擇預約方式", weight = "bold" } 
                        }
                    },
                    new {
                        type = "button",
                        style = "primary",
                        action = new { type = "message", label = "即時預約", text = "即時預約" }
                    },
                    new {
                        type = "button",
                        style = "secondary",
                        action = new { type = "message", label = "預訂時間", text = "預訂時間" }
                    }
                        }
                    }
                }
            };
            return Newtonsoft.Json.JsonConvert.SerializeObject(flex);
        }



        // Step 2: 輸入時間
        public static string BuildDepartureTimeOptions(DateTime baseTime)
        {
            var now = DateTime.Now;
            var start = baseTime < now ? now : baseTime;
            var day = start.Date;

            // 08:00 ~ 18:00 之間取整點
            var slots = Enumerable.Range(8, 11)
                .Select(h => new DateTime(day.Year, day.Month, day.Day, h, 0, 0))
                .Where(t => t >= start)
                .ToList();

            if (!slots.Any())
            {
                day = day.AddDays(1);
                slots = Enumerable.Range(8, 11)
                    .Select(h => new DateTime(day.Year, day.Month, day.Day, h, 0, 0))
                    .ToList();

            }

            var buttons = slots.Take(10).Select(t => new
            {
                type = "button",
                style = "primary",
                height = "sm",
                action = new
                {
                    type = "message",
                    label = t.ToString("HH:mm"),
                    text = $"出發時間 {t:yyyy/MM/dd HH:mm}"
                }
            }).ToList();

            // 手動輸入
            buttons.Add(new
            {
                type = "button",
                style = "secondary",
                height = "sm",
                action = new
                {
                    type = "message",
                    label = "✏️ 手動輸入時間",
                    text = "手動輸入出發時間"
                }
            });

            var bubble = new
            {
                type = "bubble",
                body = new
                {
                    type = "box",
                    layout = "vertical",
                    spacing = "md",
                    paddingAll = "20px",
                    contents = new List<object>
            {
                new { type = "text", text = "請選擇出發時間"   },
                new { type = "box", layout = "vertical", spacing = "sm", contents = buttons }
            }
                }
            };

            var flex = new { type = "flex", altText = "請選擇出發時間", contents = bubble };
            return Newtonsoft.Json.JsonConvert.SerializeObject(flex);
        }

        public static string BuildArrivalTimeOptions(DateTime baseTime)
        {
            // 抵達時間必須晚於現在 + 10 分鐘，並且取整點
            var now = DateTime.Now.AddMinutes(10);
            var start = baseTime < now ? now : baseTime;
            var firstSlot = new DateTime(start.Year, start.Month, start.Day, start.Hour, 0, 0);

            // 如果剛好跨整點前幾分鐘，往下一整點取
            if (start.Minute > 0)
                firstSlot = firstSlot.AddHours(1);

            // 最多顯示未來 10 小時的整點
            var slots = new List<DateTime>();
            for (var t = firstSlot; t <= firstSlot.AddHours(10); t = t.AddHours(1))
                slots.Add(t);

            // 建立按鈕清單
            var buttons = slots.Select(t => new
            {
                type = "button",
                style = "primary",
                height = "sm",
                action = new
                {
                    type = "message",
                    label = t.ToString("HH:mm"),
                    text = $"抵達時間 {t:yyyy/MM/dd HH:mm}"
                }
            }).ToList();

            // 加一個「✏️ 手動輸入時間」
            buttons.Add(new
            {
                type = "button",
                style = "secondary",
                height = "sm",
                action = new
                {
                    type = "message",
                    label = "✏️ 手動輸入時間",
                    text = "手動輸入抵達時間"
                }
            });

            // 組 Flex bubble
            var bubble = new
            {
                type = "bubble",
                body = new
                {
                    type = "box",
                    layout = "vertical",
                    spacing = "md",
                    paddingAll = "20px",
                    contents = new List<object>
            {
                new { type = "text", text = "請選擇抵達時間" },
                new { type = "box", layout = "vertical", spacing = "sm", contents = buttons }
            }
                }
            };

            var flex = new { type = "flex", altText = "請選擇抵達時間", contents = bubble };
            return Newtonsoft.Json.JsonConvert.SerializeObject(flex);
        }

        // Step 3: 輸入事由
        public static string BuildStep3()
        {
            var body = new List<object>
    {
        new {
            type = "box",
            layout = "vertical",
            contents = new object[] {
                new { type = "text", text = "請輸入用車事由" },
                new { type = "text", text = "例如：「送文件至台北分署」" }
            }
        }
    };

            var bubble = LineFlexBuilder.Bubble(LineFlexBuilder.Box("vertical", body));
            return LineFlexBuilder.ToJson(bubble, "請輸入用車事由");
        }

        // Step 4: 乘客人數
        public static string BuildStep4()
        {
            var body = new List<object>
            {
                LineFlexBuilder.Text("請選擇乘客人數", "bold", null, "#0f172a"),
                LineFlexBuilder.Separator(),
                LineFlexBuilder.Button("1人", "action=setPassengerCount&value=1", "primary", "#3b82f6"),
                LineFlexBuilder.Button("2人", "action=setPassengerCount&value=2", "primary", "#3b82f6"),
                LineFlexBuilder.Button("3人", "action=setPassengerCount&value=3", "primary", "#3b82f6"),
                LineFlexBuilder.Button("4人", "action=setPassengerCount&value=4", "primary", "#3b82f6")
            };

            var bubble = LineFlexBuilder.Bubble(LineFlexBuilder.Box("vertical", body));
            return LineFlexBuilder.ToJson(bubble, "請選擇乘客人數");
        }


        // Step 5-1: 輸入出發地點
        public static string BuildStep5_Origin()
        {
            var body = new List<object>
    {
        LineFlexBuilder.Text("請輸入出發地點"),
        LineFlexBuilder.Separator(),
        LineFlexBuilder.Text("例如：「台中市政府」")
    };

            var bubble = LineFlexBuilder.Bubble(LineFlexBuilder.Box("vertical", body));
            return LineFlexBuilder.ToJson(bubble, "請輸入出發地點");
        }

        // Step 5-2: 輸入前往地點
        public static string BuildStep5_Destination()
        {
            var body = new List<object>
    {
        LineFlexBuilder.Text("請輸入前往地點"),
        LineFlexBuilder.Separator(),
        LineFlexBuilder.Text("例如：「台北林區管理處」")
    };

            var bubble = LineFlexBuilder.Bubble(LineFlexBuilder.Box("vertical", body));
            return LineFlexBuilder.ToJson(bubble, "請輸入前往地點");
        }

        // Step 6: 裝載物料品名

        public static string BuildStep6()
        {
            var body = new List<object>
            {
                LineFlexBuilder.Text("請輸入裝載物品名稱", "bold", null, "#0f172a"),
                LineFlexBuilder.Separator(),
                LineFlexBuilder.Text("格式例如：「文件、筆電」")
            };
            var bubble = LineFlexBuilder.Bubble(LineFlexBuilder.Box("vertical", body));
            return LineFlexBuilder.ToJson(bubble, "請輸入裝載物品名稱");
        }

        // Step 7: 行程類型 單程 or 來回
        public static string BuildStep7()
        {
            var body = new List<object>
    {
        LineFlexBuilder.Text("請選擇行程類型", "bold", null, "#0f172a"),
        LineFlexBuilder.Separator(),
        LineFlexBuilder.Button("單程", "action=setTripType&value=single", "primary", "#3b82f6"),
        LineFlexBuilder.Button("來回", "action=setTripType&value=round", "secondary", "#22c55e")
    };

            var bubble = LineFlexBuilder.Bubble(LineFlexBuilder.Box("vertical", body));
            return LineFlexBuilder.ToJson(bubble, "請選擇行程類型");
        }

        // Step 8: 確認申請資訊
        public static string BuildStep8(BookingStateDto state)
        {
            var info = new List<object>
            {
                LineFlexBuilder.Text("請確認派車資訊"),
                LineFlexBuilder.Separator(),
                LineFlexBuilder.Text($"■ 預約時間：{state.DepartureTime}", null, "sm"),
                LineFlexBuilder.Text($"■ 乘客人數：{state.PassengerCount ?? 1}", null, "sm"),
                LineFlexBuilder.Text($"■ 事由：{state.Reason}", null, "sm"),
                LineFlexBuilder.Text($"■ 路線：{state.Origin} → {state.Destination}", null, "sm"),
                LineFlexBuilder.Text($"■ 行程：{(state.TripType == "round" ? "來回" : "單程")}", null, "sm")

            };

            var footer = new List<object>
            {
                LineFlexBuilder.Button("取消", "action=cancelApplication", "secondary"),
                LineFlexBuilder.Button("確認送出", "action=confirmApplication", "primary")
            };

            var bubble = LineFlexBuilder.Bubble(
                LineFlexBuilder.Box("vertical", info),
                LineFlexBuilder.Box("horizontal", footer)
            );

            return LineFlexBuilder.ToJson(bubble, "確認派車申請");
        }
    }
}
