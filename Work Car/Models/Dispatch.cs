using System.ComponentModel.DataAnnotations;

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
        public string DispatchStatus { get; set; } = "待派車";

        // 導覽屬性
        public virtual CarApply? Application { get; set; }
        public virtual Vehicle? Vehicle { get; set; }
        public virtual Driver? Driver { get; set; }
    }
}
