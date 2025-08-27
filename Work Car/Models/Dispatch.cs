using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cars.Models
{
    public class Dispatch
    {
        [Key]
        public int DispatchId { get; set; }

        // 外鍵
        public int ApplyId { get; set; }
        public int? DriverId { get; set; }
        public int? VehicleId { get; set; }

        // 派車狀態
        public string DispatchStatus { get; set; } = "已派車";
        
        public DateTime? DispatchTime { get; set; }

        // 時間
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // 導覽屬性
        [ForeignKey("ApplyId")]
        public CarApply CarApply { get; set; }


        public virtual Vehicle? Vehicle { get; set; }
        public virtual Driver? Driver { get; set; }
    }
}

