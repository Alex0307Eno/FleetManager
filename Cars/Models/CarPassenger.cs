using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cars.Models
{
    public class CarPassenger
    {
        [Key]
        public int PassengerId { get; set; }          // 主鍵
        public string? Name { get; set; }             // 搭乘人員姓名

        // 外鍵 (指向 CarApply)
        public int ApplyId { get; set; }              // 申請單外鍵
        public string? DeptTitle { get; set; }        // 搭乘人員部門職稱

        [ForeignKey("ApplyId")] 
        public CarApplication? CarApply { get; set; } // 導覽屬性
    }
}
