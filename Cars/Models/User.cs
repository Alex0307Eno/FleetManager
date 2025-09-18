using System;
using System.ComponentModel.DataAnnotations;

namespace Cars.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }                           // 主鍵

        public string ? LineUserId { get; set; }                  // LINE userId (U 開頭)，可為 null

        [Required, StringLength(50)]
        public string Account { get; set; } = string.Empty;       // 帳號

        [Required]
        public string PasswordHash { get; set; } = string.Empty;  // 密碼雜湊值

        [StringLength(100)]
        public string? DisplayName { get; set; }                  // 顯示名稱



        [StringLength(50)]
        public string Role { get; set; } = "User";                // 角色 (預設為 "User")

        public bool IsActive { get; set; } = true;                // 是否啟用

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // 建立時間
    }

    public class LoginDto
    {
        [Required]
        public string Account { get; set; } = string.Empty;        // 帳號

        [Required]
        public string Password { get; set; } = string.Empty;        // 密碼
    }
}
