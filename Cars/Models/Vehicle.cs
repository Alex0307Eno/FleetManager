using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Cars.Models
{
    public class Vehicle
    {
        [Key]
        public int VehicleId { get; set; } // 主鍵

        [Required(ErrorMessage = "車牌必填")]
        [StringLength(20, ErrorMessage = "車牌長度不可超過 20 個字元")]
        [RegularExpression(@"^[A-Z0-9\-]{2,20}$", ErrorMessage = "車牌格式錯誤")]
        public string? PlateNo { get; set; } // 車牌

        [StringLength(50)]
        public string? Brand { get; set; } // 廠牌

        [StringLength(50)]
        public string? Model { get; set; } // 車型

        [Range(1, 99, ErrorMessage = "核定載客數必須介於 1 到 99")]
        public int? Capacity { get; set; } // 核定載客數

        [StringLength(20)]
        [RegularExpression(@"^(待用|使用中|維修|報廢)?$", ErrorMessage = "狀態不合法")]
        public string? Status { get; set; } // 車輛狀態

        [StringLength(20)]
        public string? Type { get; set; } // 車種

        public DateTime? PurchaseDate { get; set; } // 購置日期

        [Range(0, 100000000, ErrorMessage = "車輛價值超出合理範圍")]
        public decimal? Value { get; set; } // 車輛價值

        public DateTime? LicenseDate { get; set; } // 車輛領牌日期
        public DateTime? StartUseDate { get; set; } // 車輛啟用日期
        public DateTime? InspectionDate { get; set; } // 車輛下次驗車日期

        [Range(50, 10000, ErrorMessage = "排氣量不合法")]
        public int? EngineCC { get; set; } // 排氣量

        [StringLength(50)]
        [RegularExpression(@"^[A-Za-z0-9\-]*$", ErrorMessage = "引擎號碼格式錯誤")]
        public string? EngineNo { get; set; } // 引擎號碼

        [Range(1900, 2100, ErrorMessage = "年份不合法")]
        public int? Year { get; set; } // 出廠年份

        [StringLength(50)]
        public string? Source { get; set; } // 車輛來源

        [StringLength(50)]
        public string? ApprovalNo { get; set; } // 核准文號

        public DateTime? RetiredDate { get; set; } // 報廢日期
        // 關聯
        public ICollection<Dispatch>? DispatchOrders { get; set; }
    }
}
