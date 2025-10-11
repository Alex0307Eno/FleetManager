using Cars.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cars.Core.Models
{
    public enum AppSource { Web, Line }

    public sealed class CreateCarApplicationRequest
    {
        // 身份來源
        public AppSource Source { get; init; }
        public int? WebUserId { get; init; }        // Web 走這個
        public string? LineUserId { get; init; }    // LINE 走這個

        // 申請內容（兩邊共用）
        public string? ApplyFor { get; init; }
        public string? VehicleType { get; init; }
        public string? PurposeType { get; init; }
        public string? ReasonType { get; init; }
        public int PassengerCount { get; init; }
        public string? ApplyReason { get; init; }
        public string Origin { get; init; } = "";
        public string Destination { get; init; } = "";
        public DateTime UseStart { get; init; }
        public DateTime UseEnd { get; init; }
        public string TripType { get; init; } = "single"; // single / round
        public List<CarPassenger>? Passengers { get; init; }
    }

}
