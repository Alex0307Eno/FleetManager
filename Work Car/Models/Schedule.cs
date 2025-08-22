namespace Cars.Models
{
    public class Schedule
    {
        public int ScheduleId { get; set; }
        public DateTime WorkDate { get; set; }  // 只取日期部分
        public string? Shift { get; set; }       // "AM" or "PM"

        public int DriverId { get; set; }
        public Driver? Driver { get; set; }
    }

}
