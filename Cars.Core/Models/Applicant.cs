using System.ComponentModel.DataAnnotations;

namespace Cars.Models
{
    public class Applicant
    {
        [Key]
        public int ApplicantId { get; set; }    // 主鍵

        [Required(ErrorMessage = "姓名必填")]
        [MaxLength(50)]
        [RegularExpression(@"^[\u4e00-\u9fa5a-zA-Z\s]+$", ErrorMessage = "姓名僅能輸入中文或英文")]
        public string Name { get; set; }        // 姓名

        [Display(Name = "生日")]
        [DataType(DataType.Date)]
        public DateTime? Birth { get; set; }    // 生日

        [MaxLength(100)]
        [Required(ErrorMessage = "部門必填")]
        [RegularExpression(@"^[\u4e00-\u9fa5a-zA-Z0-9\s\-]+$", ErrorMessage = "部門名稱格式不正確")]
        public string Dept { get; set; }        // 部門

        [MaxLength(10)]
        [RegularExpression(@"^\d{1,10}$", ErrorMessage = "分機僅能輸入數字")]
        public string Ext { get; set; }         // 分機

        [MaxLength(100)]
        [EmailAddress(ErrorMessage = "電子郵件格式不正確")]
        public string Email { get; set; }       // 電子郵件

        public int? UserId { get; set; }        // 外鍵
        public User User { get; set; }          // 導覽屬性

        // 對應多筆申請單
        public ICollection<CarApplication> CarApplications { get; set; }
    }
}
