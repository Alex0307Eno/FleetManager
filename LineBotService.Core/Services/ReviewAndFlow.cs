using Cars.Shared.Dtos.CarApplications;
using Cars.Shared.Dtos.Line;
using Cars.Shared.Line;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LineBotService.Core.Services
{
    public static partial class MessageBuilder
    {
        public static string BuildPendingListBubble(int page, int pageSize, List<CarApplicationDto> apps)
        {
            var contents = new List<object>
            {
                LineFlexBuilder.Text($"待審核申請單（第 {page} 頁）", "bold", "lg"),
                LineFlexBuilder.Separator()
            };

            foreach (var app in apps)
            {
                var label = $"{app.ApplicantName} - {app.ApplyReason}";
                var data = $"action=reviewApprove&applyId={app.ApplyId}";
                contents.Add(LineFlexBuilder.Button(label, data, "secondary", "#eab308"));
            }

            var body = LineFlexBuilder.Box("vertical", contents);
            var bubble = LineFlexBuilder.Bubble(body);
            return LineFlexBuilder.ToJson(bubble, "待審核清單");
        }

        public static string BuildConfirmBubble(BookingStateDto s)
        {
            var body = LineFlexBuilder.Box("vertical", new List<object>
            {
                LineFlexBuilder.Text("確認派車申請", "bold", "lg"),
                LineFlexBuilder.Separator(),
                LineFlexBuilder.Text($"出發：{s.DepartureTime}"),
                LineFlexBuilder.Text($"抵達：{s.ArrivalTime}"),
                LineFlexBuilder.Text($"事由：{s.Reason}"),
                LineFlexBuilder.Text($"人數：{s.PassengerCount}"),
                LineFlexBuilder.Text($"地點：{s.Origin} → {s.Destination}"),
                LineFlexBuilder.Separator(),
                LineFlexBuilder.Button("確認", "action=confirmApplication"),
                LineFlexBuilder.Button("取消", "action=cancelApplication", "secondary", "#f87171")
            });

            var bubble = LineFlexBuilder.Bubble(body);
            return LineFlexBuilder.ToJson(bubble, "確認派車申請");
        }
    }
}
