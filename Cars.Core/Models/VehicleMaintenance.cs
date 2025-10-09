using System;
using System.ComponentModel.DataAnnotations;

namespace Cars.Models
{
    public class VehicleMaintenance
    {
        [Key]
        public int VehicleMaintenanceId { get; set; }  // 主鍵

        [Required(ErrorMessage = "必須指定車輛")]
        public int VehicleId { get; set; }              // 車輛外鍵

        [MaxLength(20, ErrorMessage = "車牌號碼最多 20 個字元")]
        [RegularExpression(@"^[A-Z0-9\-]+$", ErrorMessage = "車牌號碼格式不正確")]
        public string? VehiclePlate { get; set; }      // 車牌

        [Required(ErrorMessage = "日期必填")]
        public DateTime Date { get; set; }             // 保養日期

        [Range(0, 2000000, ErrorMessage = "里程數必須在 0 ~ 2,000,000 公里之間")]
        public int? Odometer { get; set; }             // 里程

        [Required(ErrorMessage = "保養項目必填")]
        [MaxLength(100, ErrorMessage = "保養項目最多 100 個字元")]
        [RegularExpression(@"^[\u4e00-\u9fa5A-Za-z0-9\s\-()]+$", ErrorMessage = "保養項目格式不正確")]
        public string Item { get; set; } = "";         // 保養項目

        [MaxLength(20, ErrorMessage = "單位最多 20 個字元")]
        [RegularExpression(@"^[\u4e00-\u9fa5A-Za-z0-9]+$", ErrorMessage = "單位格式不正確")]
        public string? Unit { get; set; }              // 單位（次、瓶、個…）

        [Range(0, 9999, ErrorMessage = "數量必須在 0 ~ 9999 之間")]
        public decimal? Qty { get; set; }              // 數量

        [Range(0, 1000000, ErrorMessage = "金額必須在 0 ~ 1,000,000 之間")]
        public decimal? Amount { get; set; }           // 單價

        [MaxLength(100, ErrorMessage = "廠商名稱最多 100 個字元")]
        [RegularExpression(@"^[\u4e00-\u9fa5A-Za-z0-9\s\-()]+$", ErrorMessage = "廠商名稱格式不正確")]
        public string? Vendor { get; set; }            // 承修廠商

        [MaxLength(500, ErrorMessage = "備註最多 500 個字元")]
        [RegularExpression(@"^[^<>]*$", ErrorMessage = "備註不可包含 < 或 > 符號")]
        public string? Note { get; set; }              // 備註

        public DateTime? NextDueDate { get; set; }   // 下次到期日


        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // 建立時間
    }
}
