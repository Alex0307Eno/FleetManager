using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cars.Models
{
    public class DriverDelegation
    {
        [Key]
        public int DelegationId { get; set; }           // 主鍵

        // 被代理人 (正式 Driver)
        [Required]
        public int PrincipalDriverId { get; set; }      // 外鍵
        [ForeignKey("PrincipalDriverId")]
        public virtual Driver Principal { get; set; }   // 導覽屬性

        // 代理人 (同樣在 Drivers 表)
        [Required]
        public int AgentDriverId { get; set; }          // 外鍵
        [ForeignKey("AgentDriverId")]
        public virtual Driver Agent { get; set; }       // 導覽屬性

        [Required]
        [Display(Name = "開始日期")]
        public DateTime StartDate { get; set; }         // 代理開始日期

        [Required]
        [Display(Name = "結束日期")]
        public DateTime EndDate { get; set; }           // 代理結束日期

        [MaxLength(100)]
        [Display(Name = "代理原因")]
        public string Reason { get; set; }              // 代理原因

        [Display(Name = "趟數")]
        public int TripCount { get; set; }              // 代理期間內的趟數

        [Display(Name = "公里數")]
        public int DistanceKm { get; set; }             // 代理期間內的公里數

        [Display(Name = "建立時間")]
        public DateTime CreatedAt { get; set; } = DateTime.Now; // 建立時間
    }
}
