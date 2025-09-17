using System.ComponentModel.DataAnnotations;

namespace Cars.Models
{
    public class Applicant
    {
        [Key]
        public int ApplicantId { get; set; }    // 主鍵
        public string Name { get; set; }        // 姓名
        public DateTime? Birth { get; set; }    // 生日
        public string Dept { get; set; }        // 部門
        public string Ext { get; set; }         // 分機
        public string Email { get; set; }       // 電子郵件

        public int? UserId { get; set; }        // 外鍵
        public User User { get; set; }          // 導覽屬性

        //  對應多筆申請單
        public ICollection<CarApplication> CarApplications { get; set; } // 導覽屬性
    }
}
