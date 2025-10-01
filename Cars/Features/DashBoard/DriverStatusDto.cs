namespace Cars.Features.DashBoard
{
    public class DriverStatusDto
    {
        public int? DriverId { get; set; }
        public string? DriverName { get; set; } = "";
        public string? Shift { get; set; }
        public string? PlateNo { get; set; }
        public string? ApplicantDept { get; set; }
        public string? ApplicantName { get; set; }
        public int? PassengerCount { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string StateText { get; set; } = "";
        public DateTime? RestUntil { get; set; }
        public int? RestRemainMinutes { get; set; }
    }

}
