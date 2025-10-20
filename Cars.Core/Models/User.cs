using System;
using System.ComponentModel.DataAnnotations;

namespace Cars.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }                           // 主鍵

        public string? LineUserId { get; set; }                   // LINE userId (U 開頭)，可為 null

        [Required, StringLength(50)]
        [RegularExpression(@"^[^'""<>;]*$", ErrorMessage = "帳號格式不合法")]
        public string Account { get; set; } = string.Empty;       // 帳號

        [Required]
        [RegularExpression(@"^[^'""<>;]*$", ErrorMessage = "密碼格式不合法")]
        public string PasswordHash { get; set; } = string.Empty;  // 密碼雜湊值

        [StringLength(100)]
        [RegularExpression(@"^[^'""<>;]*$", ErrorMessage = "顯示名稱不可包含特殊符號")]
        public string? DisplayName { get; set; }                  // 顯示名稱

        [StringLength(50)]
        public string Role { get; set; } = "User";                // 角色 (預設為 "User")

        public bool IsActive { get; set; } = true;                // 是否啟用
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;// 建立時間

        // 安全相關欄位
        public DateTime? LockoutEnd { get; set; }   // 鎖定到期時間
        public int? FailedLoginCount { get; set; } = 0;  // 失敗次數

        public Applicant? Applicant { get; set; }  // 一對一導覽屬性

    }

    public class LoginDto
    {
        [Required]
        [RegularExpression(@"^[^'""<>;]*$", ErrorMessage = "帳號格式不合法")]
        public string Account { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^[^'""<>;]*$", ErrorMessage = "密碼格式不合法")]
        public string Password { get; set; } = string.Empty;
    }
}
