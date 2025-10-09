using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cars.Models
{
    public class CarPassenger
    {
        [Key]
        public int PassengerId { get; set; }          // 主鍵

        [Required(ErrorMessage = "姓名必填")]
        [MaxLength(50, ErrorMessage = "姓名長度不可超過 50 字")]
        [RegularExpression(@"^[\u4e00-\u9fa5a-zA-Z\s]+$", ErrorMessage = "姓名僅能包含中文、英文與空白")]
        public string? Name { get; set; }             // 搭乘人員姓名

        [Required]
        public int ApplyId { get; set; }              // 申請單外鍵

        [MaxLength(100, ErrorMessage = "部門職稱長度不可超過 100 字")]
        [RegularExpression(@"^[\u4e00-\u9fa5a-zA-Z0-9\s\-\(\)]+$", ErrorMessage = "部門職稱格式不正確")]
        public string? DeptTitle { get; set; }        // 搭乘人員部門職稱

        [ForeignKey("ApplyId")]
        public CarApplication? CarApply { get; set; } // 導覽屬性
    }
}
