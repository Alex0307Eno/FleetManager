using Microsoft.EntityFrameworkCore;

namespace Cars.Models
{
    [Keyless] // 沒有主鍵的 View
    public class DispatchOrder
    {
        public int DispatchId { get; set; }
        public int? VehicleId { get; set; }
        public string? PlateNo { get; set; }
        public int? DriverId { get; set; }
        public string? DriverName { get; set; }
        public int? ApplyId { get; set; }
        public string? ApplicantName { get; set; }
        public string? ApplicantDept { get; set; }
        public int? PassengerCount { get; set; }

        // ⚠️ 改成 string，避免 InvalidCastException
        public string? UseDate { get; set; }
        public string? UseTime { get; set; }

        public string? Route { get; set; }
        public string? Reason { get; set; }

        // ⚠️ TripDistance 也建議用 string，因為可能存 "12 公里"
        public string? TripDistance { get; set; }

        public string? TripType { get; set; }
        public string? Status { get; set; }
    }
}
