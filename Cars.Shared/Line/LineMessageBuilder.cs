using Cars.Shared.Dtos.CarApplications;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Cars.Shared.Line
{
    public static class LineMessageBuilder
    {
        public static string BuildDoneBubble(string driverName, string carNo)
        {
            var body = LineFlexBuilder.Box("vertical", new List<object>
            {
                LineFlexBuilder.Text("已安排駕駛人員", "bold", "lg"),
                LineFlexBuilder.Separator(),
                LineFlexBuilder.Text($"■ 駕駛人：{driverName}"),
                LineFlexBuilder.Text($"■ 使用車輛：{carNo}")
            });

            var bubble = LineFlexBuilder.Bubble(body);
            return LineFlexBuilder.ToJson(bubble, "已安排駕駛人員");
        }

        public static string BuildDriverSelectBubble(int applyId, List<(int DriverId, string DriverName)> drivers)
        {
            var contents = new List<object>
            {
                LineFlexBuilder.Text("選擇駕駛人員", "bold", "lg"),
                LineFlexBuilder.Separator()
            };

            foreach (var d in drivers)
            {
                contents.Add(LineFlexBuilder.Button(
                    d.DriverName,
                    $"action=assignDriver&applyId={applyId}&driverId={d.DriverId}&driverName={d.DriverName}"
                ));
            }

            var body = LineFlexBuilder.Box("vertical", contents);
            var bubble = LineFlexBuilder.Bubble(body);
            return LineFlexBuilder.ToJson(bubble, "選擇駕駛人員");
        }

        public static string BuildManagerReviewBubble(int applyId, List<(int VehicleId, string PlateNo)> cars)
        {
            var contents = new List<object>
            {
                LineFlexBuilder.Text("選擇車輛", "bold", "lg"),
                LineFlexBuilder.Separator()
            };

            foreach (var c in cars)
            {
                contents.Add(LineFlexBuilder.Button(
                    c.PlateNo,
                    $"action=assignVehicle&applyId={applyId}&vehicleId={c.VehicleId}&plateNo={c.PlateNo}"
                ));
            }

            var body = LineFlexBuilder.Box("vertical", contents);
            var bubble = LineFlexBuilder.Bubble(body);
            return LineFlexBuilder.ToJson(bubble, "選擇車輛");
        }

        public static string BuildDriverDispatchBubble(CarApplicationDto app, string driverName, string carNo, double km, double minutes)
        {
            var body = LineFlexBuilder.Box("vertical", new List<object>
            {
                LineFlexBuilder.Text("派車任務通知", "bold", "lg"),
                LineFlexBuilder.Separator(),
                LineFlexBuilder.Text($"■ 申請人：{app.ApplicantName}"),
                LineFlexBuilder.Text($"■ 用車事由：{app.ApplyReason}"),
                LineFlexBuilder.Text($"■ 行程：{app.Origin} → {app.Destination}"),
                LineFlexBuilder.Text($"■ 駕駛：{driverName}"),
                LineFlexBuilder.Text($"■ 車輛：{carNo}"),
                LineFlexBuilder.Text($"■ 距離：約 {km:F1} 公里 / {LineMessageBuilder.ToHourMinuteString(minutes)}")
            });

            var bubble = LineFlexBuilder.Bubble(body);
            return LineFlexBuilder.ToJson(bubble, "派車任務通知");
        }

        public static string ToHourMinuteString(double minutes)
        {
            int total = (int)System.Math.Round(minutes);
            return total >= 60
                ? $"{total / 60} 小時 {total % 60} 分鐘"
                : $"{total} 分鐘";
        }

        public static string BuildDepartureTimeFlex(DateTime baseDay, int intervalMinutes = 30)
        {
            var now = DateTime.Now.AddMinutes(5);
            var times = new List<DateTime>();
            for (int i = 8; i <= 18; i++)
            {
                for (int m = 0; m < 60; m += intervalMinutes)
                {
                    var t = new DateTime(baseDay.Year, baseDay.Month, baseDay.Day, i, m, 0);
                    if (t >= now) times.Add(t);
                }
            }

            var body = new List<object> { LineFlexBuilder.Text("請選擇出發時間", "bold", "#0f172a") };

            foreach (var t in times.Take(10))
                body.Add(LineFlexBuilder.Button($"{t:HH:mm}", $"action=setReserveTime&value={t:yyyyMMddHHmm}", "primary", "#3b82f6"));

            var bubble = LineFlexBuilder.Bubble(LineFlexBuilder.Box("vertical", body));
            return LineFlexBuilder.ToJson(bubble, "請選擇出發時間");
        }

    }
}
