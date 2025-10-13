using System;

namespace Cars.Shared.Dtos.DashBoard
{
    public class TodayScheduleDto
    {
        public int ScheduleId { get; set; }
        public string Shift { get; set; } = "";
        public int DriverId { get; set; }
        public string DriverName { get; set; } = "";
        public bool HasDispatch { get; set; }
        public DateTime? UseStart { get; set; }
        public DateTime? UseEnd { get; set; }
        public string Route { get; set; } = "";
        public string? ApplicantName { get; set; }
        public string? ApplicantDept { get; set; }
        public int PassengerCount { get; set; }
        public string? PlateNo { get; set; }
        public decimal TripDistance { get; set; }
    }
}
