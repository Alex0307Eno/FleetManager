namespace Cars.Models
{
    public class Schedule
    {
        public int ScheduleId { get; set; }
        public DateTime WorkDate { get; set; }  
        public string? Shift { get; set; }       

        public int DriverId { get; set; }
        public Driver? Driver { get; set; }
    }

}
