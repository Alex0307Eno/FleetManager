namespace Cars.Models
{
    public class Schedule
    {
        public int ScheduleId { get; set; }   // 主鍵
        public DateTime WorkDate { get; set; }// 工作日期
        public string? Shift { get; set; }    // 班別   
        public int DriverId { get; set; }     // 外鍵
        public Driver? Driver { get; set; }   // 導覽屬性

        public bool IsPresent { get; set; }  // 0=未出勤, 1=已出勤
    }

}
