using System.ComponentModel.DataAnnotations;

namespace Cars.Models
{
    public class LineUser
    {
        [Key]
        [MaxLength(50)]
        public string LineUserId { get; set; }   // LINE userId (U 開頭)

        [MaxLength(100)]
        public string DisplayName { get; set; }  // 使用者暱稱 (可選)

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
