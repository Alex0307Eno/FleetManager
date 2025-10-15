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
            var body = new
            {
                type = "box",
                layout = "vertical",
                contents = new object[]
                {
            new { type = "text", text = "請選擇預約方式", weight = "bold", size = "lg", color = "#0f172a" },
            new
            {
                type = "button",
                style = "primary",
                color = "#22c55e",
                margin = "md",
                action = new { type = "message", label = "即時預約", text = "即時預約" }
            },
            new
            {
                type = "button",
                style = "secondary",
                color = "#94a3b8",
                margin = "sm",
                action = new { type = "message", label = "預訂時間", text = "預訂時間" }
            }
                }
            };

            var bubble = new
            {
                type = "bubble",
                body = body
            };

            var flex = new
            {
                type = "flex",
                altText = "請選擇預約方式",
                contents = bubble
            };

            return Newtonsoft.Json.JsonConvert.SerializeObject(flex);
        }


        // Step 2: 輸入時間（以 QuickReply 方式為主，可自訂時間）
        public static string BuildStep2(DateTime baseDay)
        {
            var now = DateTime.Now.AddMinutes(5);
            var slots = Enumerable.Range(8, 10)
                .Select(h => new DateTime(baseDay.Year, baseDay.Month, baseDay.Day, h, 0, 0))
                .Where(t => t >= now)
                .Take(8)
                .ToList();

            var buttons = slots.Select(t =>
                LineFlexBuilder.Button($"{t:HH:mm}", $"action=setReserveTime&value={t:yyyyMMddHHmm}", "primary", "#3b82f6")
            ).ToList<object>();

            buttons.Add(LineFlexBuilder.Button("手動輸入", "action=setReserveTime&value=manual", "secondary", "#94a3b8"));

            var bubble = LineFlexBuilder.Bubble(LineFlexBuilder.Box("vertical", buttons));
            return LineFlexBuilder.ToJson(bubble, "請選擇預約時間");
        }

        // Step 3: 乘客人數
        public static string BuildStep3()
        {
            var body = new List<object>
            {
                LineFlexBuilder.Text("請選擇乘客人數", "bold", "lg", "#0f172a"),
                LineFlexBuilder.Separator(),
                LineFlexBuilder.Button("1人", "action=setPassengerCount&value=1", "primary", "#3b82f6"),
                LineFlexBuilder.Button("2人", "action=setPassengerCount&value=2", "primary", "#3b82f6"),
                LineFlexBuilder.Button("3人", "action=setPassengerCount&value=3", "primary", "#3b82f6"),
                LineFlexBuilder.Button("4人", "action=setPassengerCount&value=4", "primary", "#3b82f6")
            };

            var bubble = LineFlexBuilder.Bubble(LineFlexBuilder.Box("vertical", body));
            return LineFlexBuilder.ToJson(bubble, "請選擇乘客人數");
        }

        // Step 4: 輸入事由
        public static string BuildStep4()
        {
            var body = new List<object>
            {
                LineFlexBuilder.Text("請輸入用車事由", "bold", "lg", "#0f172a"),
                LineFlexBuilder.Separator(),
                LineFlexBuilder.Text("請直接輸入文字，例如：「送文件至台北分署」", "sm", "#64748b")
            };

            var bubble = LineFlexBuilder.Bubble(LineFlexBuilder.Box("vertical", body));
            return LineFlexBuilder.ToJson(bubble, "請輸入用車事由");
        }

        // Step 5: 出發與目的地
        public static string BuildStep5()
        {
            var body = new List<object>
            {
                LineFlexBuilder.Text("請輸入出發地與目的地", "bold", "lg", "#0f172a"),
                LineFlexBuilder.Separator(),
                LineFlexBuilder.Text("格式例如：「從台中市政府 到 南投林區管理處」", "sm", "#64748b")
            };

            var bubble = LineFlexBuilder.Bubble(LineFlexBuilder.Box("vertical", body));
            return LineFlexBuilder.ToJson(bubble, "請輸入出發地與目的地");
        }

        // Step 6: 行程類型 單程 or 來回
        public static string BuildStep6()
        {
            var body = new List<object>
            {
                LineFlexBuilder.Text("請選擇行程類型", "bold", "lg", "#0f172a"),
                LineFlexBuilder.Separator(),
                LineFlexBuilder.Button("單程", "action=setTripType&value=oneway", "primary", "#3b82f6"),
                LineFlexBuilder.Button("來回", "action=setTripType&value=roundtrip", "secondary", "#22c55e")
            };

            var bubble = LineFlexBuilder.Bubble(LineFlexBuilder.Box("vertical", body));
            return LineFlexBuilder.ToJson(bubble, "請選擇行程類型");
        }

        // Step 7: 確認申請資訊
        public static string BuildStep7(BookingStateDto state)
        {
            var info = new List<object>
            {
                LineFlexBuilder.Text("請確認派車資訊", "bold", "lg", "#0f172a"),
                LineFlexBuilder.Separator(),
                LineFlexBuilder.Text($"■ 預約時間：{state.ReserveTime}", "sm"),
                LineFlexBuilder.Text($"■ 乘客人數：{state.PassengerCount ?? 1}", "sm"),
                LineFlexBuilder.Text($"■ 事由：{state.Reason}", "sm"),
                LineFlexBuilder.Text($"■ 路線：{state.Origin} → {state.Destination}", "sm"),
                LineFlexBuilder.Text($"■ 行程：{(state.TripType == "round" ? "來回" : "單程")}", "sm")
            };

            var footer = new List<object>
            {
                LineFlexBuilder.Button("取消", "action=cancelApplication", "secondary", "#ef4444"),
                LineFlexBuilder.Button("確認送出", "action=confirmApplication", "primary", "#22c55e")
            };

            var bubble = LineFlexBuilder.Bubble(
                LineFlexBuilder.Box("vertical", info),
                LineFlexBuilder.Box("horizontal", footer)
            );

            return LineFlexBuilder.ToJson(bubble, "確認派車申請");
        }
    }
}
