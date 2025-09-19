using System.ComponentModel.DataAnnotations;

namespace Cars.Models
{
    public class Driver
    {
        [Key]
        public int DriverId { get; set; }   // 主鍵

        [Required(ErrorMessage = "姓名必填")]
        [MaxLength(50)]
        [RegularExpression(@"^[\u4e00-\u9fa5a-zA-Z\s]+$", ErrorMessage = "姓名僅能輸入中文或英文")]
        public string? DriverName { get; set; } // 駕駛姓名

        [Display(Name = "身分證字號"), MaxLength(10)]
        [Required(ErrorMessage = "身分證必填")]
        [RegularExpression(@"^[A-Z][12]\d{8}$", ErrorMessage = "身分證格式不正確")]
        public string? NationalId { get; set; } // 身分證字號 (台灣格式)

        [Display(Name = "出生年月日")]
        [Required(ErrorMessage = "生日必填")]
        [DataType(DataType.Date)]
        public DateTime? BirthDate { get; set; } // 出生年月日

        [Display(Name = "戶籍地址"), MaxLength(200)]
        [RegularExpression(@"^[\u4e00-\u9fa5a-zA-Z0-9\s\-#]+$", ErrorMessage = "地址格式不正確")]
        public string? HouseholdAddress { get; set; } // 戶籍地址

        [Display(Name = "聯絡地址"), MaxLength(200)]
        [Required(ErrorMessage = "聯絡地址必填")]
        [RegularExpression(@"^[\u4e00-\u9fa5a-zA-Z0-9\s\-#]+$", ErrorMessage = "地址格式不正確")]
        public string? ContactAddress { get; set; } // 聯絡地址

        [Display(Name = "市話"), MaxLength(20)]
        [RegularExpression(@"^\d{6,15}$", ErrorMessage = "市話格式不正確 (僅數字)")]
        public string? Phone { get; set; } // 市話

        [Display(Name = "行動電話"), MaxLength(20)]
        [Required(ErrorMessage = "手機必填")]
        [RegularExpression(@"^09\d{8}$", ErrorMessage = "手機格式不正確 (例如：0912345678)")]
        public string? Mobile { get; set; } // 行動電話

        [Display(Name = "緊急聯絡人"), MaxLength(50)]
        [Required(ErrorMessage = "緊急聯絡人必填")]
        [RegularExpression(@"^[\u4e00-\u9fa5a-zA-Z\s]+$", ErrorMessage = "聯絡人僅能輸入中文或英文")]
        public string? EmergencyContactName { get; set; } // 緊急聯絡人

        [Display(Name = "緊急聯絡電話"), MaxLength(20)]
        [RegularExpression(@"^09\d{8}$|^\d{6,15}$", ErrorMessage = "緊急聯絡電話格式不正確")]
        public string? EmergencyContactPhone { get; set; } // 緊急聯絡電話

        public bool IsAgent { get; set; } // 是否為代理駕駛

        // 關聯 (一個駕駛可以有多筆派車單)
        public virtual ICollection<Dispatch>? Dispatches { get; set; }

        public int? UserId { get; set; } // 外鍵
    }
}
