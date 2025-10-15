using Cars.Shared.Dtos.CarApplications;
using Cars.Shared.Dtos.Line;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cars.Shared.Line
{
    public static class ManagerTemplate
    {
        /// <summary>
        /// 單筆派車申請通知卡片
        /// </summary>
        public static string BuildManagerReviewBubble(CarApplicationDto a)
        {
            var body = new List<object>
            {
                LineFlexBuilder.Text("🚗 新派車申請", "bold", "lg", "#0f172a"),
                LineFlexBuilder.Text($"申請人：{a.ApplicantName ?? "—"}", "sm", "#334155"),
                LineFlexBuilder.Text($"時間：{a.UseStart:MM/dd HH:mm} - {a.UseEnd:HH:mm}", "sm", "#334155"),
                LineFlexBuilder.Text($"路線：{(a.Origin ?? "公司")} → {a.Destination ?? "未填寫"}", "sm", "#475569"),
                LineFlexBuilder.Text($"乘客：{a.PassengerCount ?? 1} 人｜行程：{(a.TripType == "round" ? "來回" : "單程")}", "sm", "#475569"),
                LineFlexBuilder.Text($"事由：{a.ApplyReason ?? "—"}", "sm", "#64748b"),
                LineFlexBuilder.Separator()
            };

            var footer = new List<object>
            {
                LineFlexBuilder.Button("❌ 駁回", $"action=reviewReject&applyId={a.ApplyId}", "secondary", "#ef4444"),
                LineFlexBuilder.Button("✅ 同意", $"action=reviewApprove&applyId={a.ApplyId}", "primary", "#22c55e")
            };

            var bubble = LineFlexBuilder.Bubble(
                LineFlexBuilder.Box("vertical", body),
                LineFlexBuilder.Box("horizontal", footer)
            );

            return LineFlexBuilder.ToJson(bubble, "派車申請通知");
        }

        /// <summary>
        /// 多筆待審清單（含分頁按鈕）
        /// </summary>
        public static string BuildPendingListBubble(int page, int pageSize, List<CarApplicationDto> apps)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 5;

            var pending = apps
                .Where(a => string.Equals(a.Status, "待審核", StringComparison.OrdinalIgnoreCase))
                .OrderBy(a => a.UseStart)
                .ToList();

            if (!pending.Any())
                return JsonConvert.SerializeObject(new { type = "text", text = "目前沒有待審核申請單" });

            var totalPages = (int)Math.Ceiling(pending.Count / (double)pageSize);
            var items = pending.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var contents = new List<object> { LineFlexBuilder.Text($"🚗 待審核清單（第 {page}/{totalPages} 頁）", "bold", "md") };

            foreach (var a in items)
            {
                contents.Add(LineFlexBuilder.Separator());
                contents.Add(LineFlexBuilder.Text($"申請人：{a.ApplicantName ?? "—"}", "sm", "#334155"));
                contents.Add(LineFlexBuilder.Text($"時間：{a.UseStart:MM/dd HH:mm} - {a.UseEnd:HH:mm}", "sm", "#334155"));
                contents.Add(LineFlexBuilder.Text($"路線：{(a.Origin ?? "公司")} → {a.Destination ?? "未填寫"}", "sm", "#475569"));
                contents.Add(LineFlexBuilder.Text($"事由：{a.ApplyReason ?? "—"}", "sm", "#64748b"));
                contents.Add(LineFlexBuilder.Box("horizontal", new List<object>
                {
                    LineFlexBuilder.Button("駁回", $"action=reviewReject&applyId={a.ApplyId}", "secondary", "#ef4444"),
                    LineFlexBuilder.Button("同意", $"action=reviewApprove&applyId={a.ApplyId}", "primary", "#22c55e")
                }));
            }

            var footer = new List<object>();
            if (page > 1)
                footer.Add(LineFlexBuilder.Button("⬅️ 上一頁", $"action=reviewListPage&page={page - 1}", "secondary", "#94a3b8"));
            footer.Add(LineFlexBuilder.Text($"第 {page}/{totalPages} 頁", "sm", "#64748b"));
            if (page < totalPages)
                footer.Add(LineFlexBuilder.Button("下一頁 ➡️", $"action=reviewListPage&page={page + 1}", "secondary", "#94a3b8"));

            var bubble = LineFlexBuilder.Bubble(
                LineFlexBuilder.Box("vertical", contents),
                LineFlexBuilder.Box("horizontal", footer)
            );

            return LineFlexBuilder.ToJson(bubble, "待審核派車清單");
        }
    }

}
