using System;
using System.ComponentModel.DataAnnotations;

namespace Cars.Models
{
    public class LineUser
    {
        [Key]
        [MaxLength(50)]
        [Required(ErrorMessage = "LineUserId 必填")]
        [RegularExpression(@"^U[0-9a-fA-F]{32}$", ErrorMessage = "LineUserId 格式不正確")]
        public string LineUserId { get; set; }   // LINE userId 

        [MaxLength(100)]
        [RegularExpression(@"^[\u4e00-\u9fa5A-Za-z0-9 _\-]+$", ErrorMessage = "暱稱只能包含中英文、數字與常見符號")]
        public string? DisplayName { get; set; } // 使用者暱稱 

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
