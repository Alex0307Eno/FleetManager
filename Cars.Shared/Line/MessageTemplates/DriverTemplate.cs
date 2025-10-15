using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Cars.Shared.Line
{
    public static class DriverTemplate
    {
        /// <summary>
        /// 建立「選擇駕駛人」的 Flex Bubble
        /// </summary>
        public static string BuildDriverSelectBubble(int applyId, List<(int DriverId, string DriverName)> drivers)
        {
            if (drivers == null || !drivers.Any())
                return LineFlexBuilder.ToJson(new { type = "text", text = "⚠️ 沒有可用的駕駛人" });

            var contents = new List<object>
            {
                LineFlexBuilder.Text("請選擇駕駛人", "bold", "lg", "#0f172a")
            };

            contents.AddRange(drivers.Select(d =>
                LineFlexBuilder.Button(
                    label: d.DriverName,
                    data: $"action=assignDriver&applyId={applyId}&driverId={d.DriverId}&driverName={d.DriverName}",
                    style: "primary",
                    color: "#22c55e"
                )
            ));

            var bubble = LineFlexBuilder.Bubble(LineFlexBuilder.Box("vertical", contents));
            return LineFlexBuilder.ToJson(bubble, "選擇駕駛人");
        }

        /// <summary>
        /// 建立「選擇車輛」的 Flex Bubble
        /// </summary>
        public static string BuildCarSelectBubble(int applyId, List<(int VehicleId, string PlateNo)> cars)
        {
            if (cars == null || !cars.Any())
                return LineFlexBuilder.ToJson(new { type = "text", text = "⚠️ 無可用車輛" });

            var contents = new List<object>
            {
                LineFlexBuilder.Text("請選擇車輛", "bold", "lg", "#0f172a")
            };

            contents.AddRange(cars.Select(c =>
                LineFlexBuilder.Button(
                    label: c.PlateNo,
                    data: $"action=assignVehicle&applyId={applyId}&vehicleId={c.VehicleId}&plateNo={c.PlateNo}",
                    style: "secondary",
                    color: "#3b82f6"
                )
            ));

            var bubble = LineFlexBuilder.Bubble(LineFlexBuilder.Box("vertical", contents));
            return LineFlexBuilder.ToJson(bubble, "選擇車輛");
        }

        /// <summary>
        /// 建立「完成派車」通知 Bubble
        /// </summary>
        public static string BuildDoneBubble(string driverName, string carNo)
        {
            var contents = new List<object>
            {
                LineFlexBuilder.Text("✅ 已安排駕駛人員", "bold", "lg", "#0f172a"),
                LineFlexBuilder.Separator(),
                LineFlexBuilder.Text($"駕駛人：{driverName}", size: "sm"),
                LineFlexBuilder.Text($"使用車輛：{carNo}", size: "sm")
            };

            var bubble = LineFlexBuilder.Bubble(LineFlexBuilder.Box("vertical", contents));
            return LineFlexBuilder.ToJson(bubble, "已安排駕駛人員");
        }
    }
}
