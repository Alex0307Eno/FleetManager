using System.ComponentModel.DataAnnotations;

namespace Cars.Models
{
    public class VehicleMaintenance
    {
        [Key]
        public int VehicleMaintenanceId { get; set; }  // 主鍵

        // 車輛
        public int VehicleId { get; set; }              // 車輛外鍵
        public string? VehiclePlate { get; set; }      // 車牌

        // 保養資料
        public DateTime Date { get; set; }            // 日期
        public int? Odometer { get; set; }            // 里程
        public string Item { get; set; } = "";        // 保養項目
        public string? Unit { get; set; }             // 單位（次、瓶、個…）
        public decimal? Qty { get; set; }             // 數量
        public decimal? Amount { get; set; }          // 單價
        public string? Vendor { get; set; }           // 承修廠商
        public string? Note { get; set; }             // 備註

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // 建立時間
    }
}
