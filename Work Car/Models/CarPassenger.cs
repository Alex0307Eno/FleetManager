using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cars.Models
{
    public class CarPassenger
    {
        [Key]
        public int PassengerId { get; set; }   // 主鍵

        [Required]
        public string? Name { get; set; }  // 搭乘人員姓名

        // 外鍵 (指向 CarApply)
        public int ApplyId { get; set; }

        [ForeignKey("ApplyId")]
        public CarApply? CarApply { get; set; }
    }
}
