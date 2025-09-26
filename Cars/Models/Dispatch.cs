using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cars.Models
{
    public class Dispatch
    {
        [Key]
        public int DispatchId { get; set; }                         // 主鍵

        // 外鍵
        public int ApplyId { get; set; }                            // 申請單外鍵
        public int? DriverId { get; set; }                          // 司機外鍵
        public int? VehicleId { get; set; }                         // 車輛外鍵

        // 派車狀態
        [Required]
        public string DispatchStatus { get; set; }                  // 派車單狀態

        public bool IsLongTrip { get; set; }                        // 是否長途
        // 時間
        public DateTime? StartTime { get; set; }                    // 實際出發時間
        public DateTime? EndTime { get; set; }                      // 實際結束時間
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // 建立時間

        // 導覽屬性
        [ForeignKey("ApplyId")]
        public CarApplication CarApplication { get; set; }                // 申請單導覽屬性
        public virtual Vehicle? Vehicle { get; set; }               // 車輛導覽屬性
        public virtual Driver? Driver { get; set; }                 // 司機導覽屬性

        // 母單 → 併入的子單
        public ICollection<DispatchLink> ChildLinks { get; set; }   // 母單連結

        // 子單 → 被併入的母單
        public ICollection<DispatchLink> ParentLinks { get; set; }  // 子單連結



    }
}

