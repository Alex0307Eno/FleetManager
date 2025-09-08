using Microsoft.EntityFrameworkCore;

namespace Cars.Models
{
    [Keyless] // View 沒有主鍵
    public class DispatchOrder
    {
        public int DispatchId { get; set; }
        public int? VehicleId { get; set; }
        public string? PlateNo { get; set; } = "未指派";
        public int? DriverId { get; set; }
        public string? DriverName { get; set; }
        public int? ApplyId { get; set; }
        public int? ApplicantId { get; set; }
        public string? ApplicantName { get; set; }   // 對應 View.Name
        public string? ApplicantDept { get; set; }   // 對應 View.Dept
        public int? PassengerCount { get; set; }
        public string? UseDate { get; set; }
        public string? UseTime { get; set; }
        public DateTime? UseStart { get; set; }
        public string? Route { get; set; }
        public string? Reason { get; set; }
        public decimal? TripDistance { get; set; }
        public string? TripType { get; set; }
        public string? Status { get; set; }
    }
}
