using System.ComponentModel.DataAnnotations;

namespace Cars.Models
{
    public class LineUser
    {
        [Key]
        [MaxLength(50)]
        public string LineUserId { get; set; }   // LINE userId (U 開頭)

        [Required]
        [MaxLength(20)]
        public string Role { get; set; }         // Applicant / Admin / Driver

        public int? RelatedId { get; set; }      // 對應到系統的 ApplicantId 或 DriverId

        [MaxLength(100)]
        public string DisplayName { get; set; }  // 使用者暱稱 (可選)

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
