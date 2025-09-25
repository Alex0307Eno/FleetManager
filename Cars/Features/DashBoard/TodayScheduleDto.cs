namespace Cars.Dtos
{
    public class TodayScheduleDto
    {
        public int ScheduleId { get; set; }
        public string Shift { get; set; } = "";
        public int DriverId { get; set; }
        public string DriverName { get; set; } = "";
        public bool HasDispatch { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Route { get; set; } = "";
        public string? ApplicantName { get; set; }
        public string? ApplicantDept { get; set; }
        public int PassengerCount { get; set; }
        public string? PlateNo { get; set; }
        public decimal TripDistance { get; set; }
        public string Attendance { get; set; } = "";
    }
}
