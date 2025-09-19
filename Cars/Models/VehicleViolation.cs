using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cars.Models
{
    public class VehicleViolation
    {
        [Key]
        public int ViolationId { get; set; }         // 主鍵

        [Required(ErrorMessage = "必須指定車輛")]
        public int VehicleId { get; set; }           // 車輛外鍵

        [Required(ErrorMessage = "發生日期必填")]
        public DateTime ViolationDate { get; set; }  // 發生日期

        [MaxLength(120, ErrorMessage = "地點最多 120 個字元")]
        [RegularExpression(@"^[\u4e00-\u9fa5A-Za-z0-9\s\-()]*$", ErrorMessage = "地點格式不正確")]
        public string Location { get; set; }         // 地點

        [MaxLength(80, ErrorMessage = "違規類別最多 80 個字元")]
        [RegularExpression(@"^[\u4e00-\u9fa5A-Za-z\s]+$", ErrorMessage = "違規類別只能輸入中文或英文")]
        public string Category { get; set; }         // 例如：超速、違規停車…

        [Range(0, 12, ErrorMessage = "記點必須在 0 ~ 12 之間")]
        public int? Points { get; set; }             // 記點

        [Range(0, 1000000, ErrorMessage = "罰鍰必須在 0 ~ 1,000,000 之間")]
        public int? FineAmount { get; set; }         // 罰鍰(元)

        [MaxLength(20, ErrorMessage = "狀態最多 20 個字元")]
        [RegularExpression(@"^(未繳|已繳|申訴中)$", ErrorMessage = "狀態只能是 未繳/已繳/申訴中")]
        public string Status { get; set; }           // 未繳/已繳/申訴中

        public DateTime? DueDate { get; set; }       // 繳費期限
        public DateTime? PaidDate { get; set; }      // 繳費日期

        [MaxLength(500, ErrorMessage = "備註最多 500 個字元")]
        [RegularExpression(@"^[^<>]*$", ErrorMessage = "備註不可包含 < 或 > 符號")]
        public string Notes { get; set; }            // 備註

        // 導覽
        [ForeignKey("VehicleId")]
        public virtual Vehicle Vehicle { get; set; } // 車輛導覽屬性
    }
}
