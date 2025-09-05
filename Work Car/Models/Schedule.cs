namespace Cars.Models
{
    public class Schedule
    {
        public int ScheduleId { get; set; }
        public DateTime WorkDate { get; set; }  
        public string? Shift { get; set; }       

        public int DriverId { get; set; }
        public Driver? Driver { get; set; }

        public bool IsPresent { get; set; } // 0=未出勤, 1=已出勤
    }

}
