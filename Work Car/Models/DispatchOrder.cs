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

        public string? UseDate { get; set; }
        public string? UseTime { get; set; }

        public string? Route { get; set; }
        public string? Reason { get; set; }

        public string? TripDistance { get; set; }

        public string? TripType { get; set; }
        public string? Status { get; set; }
        public Vehicle Vehicle { get; set; }
    }
}
