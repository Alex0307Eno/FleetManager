using System.ComponentModel.DataAnnotations;

namespace Cars.Models
{
    public class DriverLineAssignment
    {
        [Key]
        public int AssignmentId { get; set; } // 主鍵
        public string LineCode { get; set; } = ""; // A~E
        public int DriverId { get; set; } // 司機編號
        public DateTime StartDate { get; set; } // 開始日期
        public DateTime? EndDate { get; set; } // 結束日期
    }

}
