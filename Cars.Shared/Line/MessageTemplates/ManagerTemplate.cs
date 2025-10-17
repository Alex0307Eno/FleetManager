using Cars.Shared.Dtos.CarApplications;
using Cars.Shared.Dtos.Drivers;
using Cars.Shared.Dtos.Line;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Json = System.Text.Json.JsonSerializer;


namespace Cars.Shared.Line
{
    public static class ManagerTemplate
    {
        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        /// <summary>
        /// 單筆派車申請通知卡片
        /// </summary>
        public static string BuildManagerReviewBubble(CarApplicationDto a)
        {
            var body = new List<object>
            {
                LineFlexBuilder.Text(
                    text: "🚗 新派車申請",
                    size: "md",
                    color: "#0f172a",
                    weight: "bold"
                ),
                LineFlexBuilder.Text(
                    text: $"申請人：{a.ApplicantName ?? "—"}",
                    size: "sm",
                    color: "#334155"
                ),
                LineFlexBuilder.Text(
                    text: $"時間：{a.UseStart:MM/dd HH:mm} - {a.UseEnd:HH:mm}",
                    size: "sm",
                    color: "#334155"
                ),
                LineFlexBuilder.Text(
                    text: $"路線：{(a.Origin ?? "公司")} → {a.Destination ?? "未填寫"}",
                    size: "sm",
                    color: "#475569"
                ),
                LineFlexBuilder.Text(
                    text: $"乘客：{a.PassengerCount ?? 1} 人｜行程：{(a.TripType == "round" ? "來回" : "單程")}",
                    size: "sm",
                    color: "#475569"
                ),
                LineFlexBuilder.Text(
                    text: $"事由：{a.ApplyReason ?? "—"}",
                    size: "sm",
                    color: "#64748b"
                ),
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
        public static string BuildPendingListBubble(List<CarApplicationDto> apps, int page = 1, int pageSize = 5)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 5;

            var pending = apps
                .Where(a => string.Equals(a.Status, "待審核", StringComparison.OrdinalIgnoreCase))
                .OrderBy(a => a.UseStart)
                .ToList();

            if (!pending.Any())
            {
                return Json.Serialize(new
                {
                    type = "text",
                    text = "目前沒有待審核申請單"
                });
            }

            var totalPages = (int)Math.Ceiling(pending.Count / (double)pageSize);
            var items = pending.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var contents = new List<object>
    {
        LineFlexBuilder.Text($"🚗 待審核清單（第 {page}/{totalPages} 頁）", weight: "bold", size: "lg", color: "#1e293b"),
        new { type = "separator", margin = "md" }
    };

            foreach (var a in items)
            {
                contents.Add(LineFlexBuilder.Box("vertical", new List<object>
        {
            LineFlexBuilder.Text($"申請人：{a.ApplicantName ?? "—"}", size: "sm", color: "#334155"),
            LineFlexBuilder.Text($"時間：{a.UseStart:MM/dd HH:mm} - {a.UseEnd:HH:mm}", size: "sm", color: "#334155"),
            LineFlexBuilder.Text($"路線：{(a.Origin ?? "公司")} → {a.Destination ?? "未填寫"}", size: "sm", color: "#475569", wrap: true),
            LineFlexBuilder.Text($"事由：{a.ApplyReason ?? "—"}", size: "sm", color: "#64748b", wrap: true),
            LineFlexBuilder.Box("horizontal", new List<object>
            {
                LineFlexBuilder.Button("❌ 駁回", $"action=reviewReject&applyId={a.ApplyId}", "secondary", "#ef4444"),
                LineFlexBuilder.Button("✅ 同意", $"action=reviewApprove&applyId={a.ApplyId}", "primary", "#22c55e")
            })
        }, spacing: "sm", margin: "md"));
            }

            // 頁尾只有下一頁按鈕（如果還有資料）
            var footer = new List<object>();
            if (page < totalPages)
                footer.Add(LineFlexBuilder.Button("下一頁 ➡️", $"action=reviewListPage&page={page + 1}", "secondary", "#94a3b8"));

            var bubble = new
            {
                type = "bubble",
                body = LineFlexBuilder.Box("vertical", contents, spacing: "sm"),
                footer = footer.Any() ? LineFlexBuilder.Box("horizontal", footer, spacing: "sm") : null
            };

            var flex = new
            {
                type = "flex",
                altText = "待審核派車清單",
                contents = bubble
            };

            return Json.Serialize(flex, new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }

        /// <summary>
        /// 產生「選擇駕駛人」的 Quick Reply 訊息
        /// </summary>
        /// <param name="drivers"></param>
        /// <param name="applyId"></param>
        /// <returns></returns>
        public static string BuildDriverFlex(int applyId, IEnumerable<DriverDto> drivers)
        {
            var driverBoxes = drivers.Select(d => new
            {
                type = "box",
                layout = "horizontal",
                spacing = "md",
                contents = new object[]
                {
            new { type = "text", text = d.DriverName, weight = "bold", size = "md", flex = 3 },
            new { type = "text", text = d.IsAgent ? "代理駕駛" : "主駕", size = "sm", color = "#888888", flex = 2 },
            new
            {
                type = "button",
                style = "primary",
                height = "sm",
                action = new
                {
                    type = "postback",
                    label = "選擇",
                    data = $"action=selectDriver&applyId={applyId}&driverId={d.DriverId}"
                },
                flex = 2
            }
                }
            }).ToList();

            var bubble = new
            {
                type = "bubble",
                size = "mega",
                body = new
                {
                    type = "box",
                    layout = "vertical",
                    contents = new object[]
                    {
                new { type = "text", text = "🚗 請選擇可派駕駛", weight = "bold", size = "lg", color = "#1e293b" },
                new { type = "separator", margin = "md" },
                new { type = "box", layout = "vertical", spacing = "sm", contents = driverBoxes }
                    }
                }
            };

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                type = "flex",
                altText = "選擇駕駛",
                contents = bubble
            },
            new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
        }

        /// <summary>
        /// 通知駕駛：新任務指派
        /// </summary>
        /// <param name="driverName"></param>
        /// <param name="plateNo"></param>
        /// <param name="origin"></param>
        /// <param name="destination"></param>
        /// <param name="useStart"></param>
        /// <returns></returns>
        public static string BuildDriverAssignedBubble(string driverName, string plateNo, string origin, string destination, DateTime useStart)
        {
            var flex = new
            {
                type = "flex",
                altText = "派車任務通知",
                contents = new
                {
                    type = "bubble",
                    body = new
                    {
                        type = "box",
                        layout = "vertical",
                        contents = new object[]
                        {
                   
                    new { type = "text", text = "🚗 新任務指派", weight = "bold", color = "#0f172a" },

                    new { type = "text", text = $"駕駛：{driverName}", size = "sm", color = "#334155" },
                    new { type = "text", text = $"車牌：{plateNo}", size = "sm", color = "#334155" },
                    new { type = "text", text = $"路線：{origin} → {destination}", size = "sm", color = "#475569", wrap = true },
                    new { type = "text", text = $"出發時間：{useStart:MM/dd HH:mm}", size = "sm", color = "#64748b" },
                    new { type = "separator", margin = "md" },
                    new
                    {
                        type = "button",
                        style = "primary",
                        color = "#2563eb",
                        height = "sm",
                        action = new
                        {
                            type = "postback",
                            label = "📋 查看任務詳情",
                            data = $"action=viewDispatch&driver={driverName}"
                        }
                    }
                        }
                    }
                }
            };

            return System.Text.Json.JsonSerializer.Serialize(flex, new System.Text.Json.JsonSerializerOptions { WriteIndented = false });
        }

        /// <summary>
        /// 通知管理員：派車完成通知
        /// </summary>
        /// <param name="applicant"></param>
        /// <param name="driver"></param>
        /// <param name="plateNo"></param>
        /// <param name="origin"></param>
        /// <param name="destination"></param>
        /// <param name="start"></param>
        /// <returns></returns>
        public static string BuildManagerDispatchDoneBubble(
    string applicant, string driver, string plateNo,
    string origin, string destination, DateTime start)
        {
            var flex = new
            {
                type = "flex",
                altText = "派車完成通知",
                contents = new
                {
                    type = "bubble",
                    body = new
                    {
                        type = "box",
                        layout = "vertical",
                        contents = new object[]
                        {
         
                    new { type = "text", text = "✅ 派車完成", weight = "bold", color = "#0f172a" },
                    new { type = "text", text = $"申請人：{applicant}", size = "sm", color = "#334155" },
                    new { type = "text", text = $"駕駛：{driver}", size = "sm", color = "#334155" },
                    new { type = "text", text = $"車牌：{plateNo}", size = "sm", color = "#334155" },
                    new { type = "text", text = $"行程：{origin} → {destination}", size = "sm", color = "#475569", wrap = true },
                    new { type = "text", text = $"出發：{start:MM/dd HH:mm}", size = "sm", color = "#64748b" }
                        }
                    }
                }
            };

            return System.Text.Json.JsonSerializer.Serialize(flex, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = false
            });
        }

    }

}
