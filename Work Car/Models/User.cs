using System;
using System.ComponentModel.DataAnnotations;

namespace Cars.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required, StringLength(50)]
        public string Account { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [StringLength(100)]
        public string? DisplayName { get; set; }

        

        [StringLength(50)]
        public string Role { get; set; } = "User"; 

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class LoginDto
    {
        [Required]
        public string Account { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}
