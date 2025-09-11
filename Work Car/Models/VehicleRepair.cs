using System.ComponentModel.DataAnnotations;

namespace Cars.Models
{
    public class VehicleRepair
    {
        [Key]
        public int RepairRequestId { get; set; }        // 主鍵
        public int VehicleId { get; set; }              // 車輛外鍵
        public string PlateNo { get; set; } = "";       // 車牌
        public DateTime Date { get; set; }              // 報修日期
        public string? Place { get; set; }              // 報修地點
        public string Issue { get; set; } = "";         // 故障描述
        public string Status { get; set; } = "待處理";  // 報修狀態
        public decimal? CostEstimate { get; set; }      // 預估費用
        public string? Vendor { get; set; }             // 維修廠商
        public string? Note { get; set; }               // 備註
    }
}
