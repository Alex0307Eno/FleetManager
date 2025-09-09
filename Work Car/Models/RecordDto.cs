namespace Cars.Models
{
    public class RecordDto
    {
        public int Id { get; set; }                 // d.DispatchId

        public int ApplyId { get; set; }            // a.ApplyId
        public DateTime? UseStart { get; set; }     // a.UseStart
        public DateTime? UseEnd { get; set; }       // a.UseEnd
        public string? Route { get; set; }          // a.Route 或 Origin-Destination
        public string TripType { get; set; }
        public string? ReasonType { get; set; }     // a.ReasonType
        public string? Reason { get; set; }         // a.ApplyReason
        public string? Applicant { get; set; }      // a.ApplicantName
        public int? Seats { get; set; }             // a.Seats
        public decimal? Km { get; set; }            // 單趟/來回里程擇一
        public string? Status { get; set; } = "待派車";        // a.Status
        public string? Driver { get; set; }         // r.DriverName
        public int? DriverId { get; set; }          // r.DriverId
        public string? Plate { get; set; }          // v.PlateNo
        public int? VehicleId { get; set; }         // v.VehicleId
        public string? LongShort { get; set; }      // 若有欄位就回；沒有就 null
    }
}
