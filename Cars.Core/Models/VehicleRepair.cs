using System;
using System.ComponentModel.DataAnnotations;

namespace Cars.Models
{
    public class VehicleRepair
    {
        [Key]
        public int RepairRequestId { get; set; }        // 主鍵

        [Required(ErrorMessage = "必須指定車輛")]
        public int VehicleId { get; set; }              // 車輛外鍵

        [Required(ErrorMessage = "車牌必填")]
        [MaxLength(20, ErrorMessage = "車牌號碼最多 20 個字元")]
        [RegularExpression(@"^[A-Z0-9\-]+$", ErrorMessage = "車牌格式不正確，只能包含大寫字母、數字與 -")]
        public string PlateNo { get; set; } = "";       // 車牌

        [Required(ErrorMessage = "報修日期必填")]
        public DateTime Date { get; set; }              // 報修日期

        [MaxLength(200, ErrorMessage = "地點最多 200 個字元")]
        [RegularExpression(@"^[\u4e00-\u9fa5A-Za-z0-9\s\-()]*$", ErrorMessage = "地點格式不正確")]
        public string? Place { get; set; }              // 報修地點

        [Required(ErrorMessage = "故障描述必填")]
        [MaxLength(500, ErrorMessage = "故障描述最多 500 個字元")]
        [RegularExpression(@"^[^<>]*$", ErrorMessage = "故障描述不可包含 < 或 > 符號")]
        public string Issue { get; set; } = "";         // 故障描述

        [Required]
        [MaxLength(20, ErrorMessage = "狀態最多 20 個字元")]
        [RegularExpression(@"^[\u4e00-\u9fa5A-Za-z]+$", ErrorMessage = "狀態只能輸入中文")]
        public string Status { get; set; } = "待處理";  // 報修狀態

        [Range(0, 10000000, ErrorMessage = "費用必須在 0 ~ 10,000,000 之間")]
        public decimal? CostEstimate { get; set; }      // 預估費用

        [MaxLength(100, ErrorMessage = "維修廠商最多 100 個字元")]
        [RegularExpression(@"^[\u4e00-\u9fa5A-Za-z0-9\s\-()]*$", ErrorMessage = "廠商名稱格式不正確")]
        public string? Vendor { get; set; }             // 維修廠商

        [MaxLength(500, ErrorMessage = "備註最多 500 個字元")]
        [RegularExpression(@"^[^<>]*$", ErrorMessage = "備註不可包含 < 或 > 符號")]
        public string? Note { get; set; }               // 備註
    }
}
