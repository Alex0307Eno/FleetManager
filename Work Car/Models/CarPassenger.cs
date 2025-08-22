using System.ComponentModel.DataAnnotations;

namespace Cars.Models
{
    public class CarPassenger
    {
        [Key]
        public int PassengerId { get; set; }   // 主鍵
        public string? Name { get; set; }  // 搭乘人員姓名

        // 關聯到 CarApply
        public int ApplyId { get; set; }
        public CarApply? CarApply { get; set; }
    }
}
