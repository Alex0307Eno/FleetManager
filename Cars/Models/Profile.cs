using System;
using System.ComponentModel.DataAnnotations;

namespace Cars.Models
{
    public class Profile
    {
        [Key] // 指定主鍵
        public int UserId { get; set; }                     // 主鍵

        [Required(ErrorMessage = "帳號必填")]
        [MaxLength(50, ErrorMessage = "帳號長度不可超過 50 字")]
        [RegularExpression(@"^[A-Za-z0-9_]+$", ErrorMessage = "帳號只能包含英數字和底線")]
        public string Account { get; set; }                 // 帳號

        [MaxLength(100, ErrorMessage = "顯示名稱過長")]
        [RegularExpression(@"^[\u4e00-\u9fa5A-Za-z0-9\s\-_]+$", ErrorMessage = "顯示名稱包含非法字元")]
        public string? DisplayName { get; set; }            // 顯示名稱

        [MaxLength(100, ErrorMessage = "部門名稱過長")]
        public string? Dept { get; set; }                   // 部門

        [MaxLength(10, ErrorMessage = "分機號碼過長")]
        [RegularExpression(@"^[0-9]+$", ErrorMessage = "分機只能是數字")]
        public string? Ext { get; set; }                    // 分機

        [MaxLength(200, ErrorMessage = "電子郵件過長")]
        [EmailAddress(ErrorMessage = "電子郵件格式錯誤")]
        public string? Email { get; set; }                  // 電子郵件

        [DataType(DataType.Date)]
        public DateTime? Birth { get; set; }                // 生日
    }
}
