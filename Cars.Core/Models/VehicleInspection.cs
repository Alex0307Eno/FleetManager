using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cars.Models
{
    public class VehicleInspection
    {
        [Key]
        public int InspectionId { get; set; }       // 主鍵

        [Required(ErrorMessage = "必須指定車輛")]
        public int VehicleId { get; set; }          // 車輛外鍵

        [Required(ErrorMessage = "驗車日期必填")]
        public DateTime InspectionDate { get; set; } // 驗車日期

        [Required(ErrorMessage = "驗車站必填")]
        [MaxLength(80, ErrorMessage = "驗車站名稱最多 80 個字元")]
        [RegularExpression(@"^[\u4e00-\u9fa5A-Za-z0-9\s\-()]+$", ErrorMessage = "驗車站格式不正確")]
        public string Station { get; set; }          // 驗車站/監理站

        [Required(ErrorMessage = "驗車結果必填")]
        [MaxLength(20)]
        [RegularExpression(@"^(合格|不合格)$", ErrorMessage = "驗車結果僅能是『合格』或『不合格』")]
        public string Result { get; set; }           // 合格/不合格

        public DateTime? NextDueDate { get; set; }   // 下次到期日

        [Range(0, 2000000, ErrorMessage = "里程數必須在 0 ~ 2,000,000 公里之間")]
        public int? OdometerKm { get; set; }         // 里程

        [MaxLength(500, ErrorMessage = "備註最多 500 個字元")]
        [RegularExpression(@"^[^<>]*$", ErrorMessage = "備註不可包含特殊符號 < 或 >")]
        public string Notes { get; set; }            // 備註

        // 導覽屬性
        [ForeignKey("VehicleId")]
        public virtual Vehicle Vehicle { get; set; } // 車輛導覽屬性
    }
}
