namespace Cars.Models
{
    public class Schedule
    {
        public int ScheduleId { get; set; } // 主鍵
        public DateTime WorkDate { get; set; } // 工作日期
        public string Shift { get; set; } = "";   // AM/PM/G1/G2/G3
        public string LineCode { get; set; } = ""; // 'A'~'E'
        public int? DriverId { get; set; }         // 臨時覆寫（可 NULL）
        public Driver? Driver { get; set; } // 導航屬性
    }
}