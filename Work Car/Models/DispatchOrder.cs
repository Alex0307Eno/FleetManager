namespace Cars.Models
{
    public class DispatchOrder
    {
        public int DispatchId { get; set; }
        public DateTime UseStart { get; set; }
        public DateTime UseEnd { get; set; }
        public string? Origin { get; set; }
        public string? Destination { get; set; }
        public string? ApplyReason { get; set; }
        public string? ApplicantName { get; set; }
        public int Seats { get; set; }
        public string? TripType { get; set; } // 單程/來回
        public int? SingleDistance { get; set; }
        public int? RoundTripDistance { get; set; }
        public string? Status { get; set; }
        public int? DriverId { get; set; }
        public int? VehicleId { get; set; }
        public string? DriverName { get; set; }
        public string? PlateNo { get; set; }
    }
}
