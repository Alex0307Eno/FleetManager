using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cars.Models
{
    public class DriverDelegation
    {
        [Key]
        public int DelegationId { get; set; }

        // 代理人 (DriverAgent)
        public int AgentId { get; set; }
        [ForeignKey("AgentId")]
        public virtual DriverAgent Agent { get; set; }

        // 被代理人 (Driver)
        public int? PrincipalDriverId { get; set; }
        [ForeignKey("PrincipalDriverId")]
        public virtual Driver Principal { get; set; }

        [Required]
        [Display(Name = "開始日期")]
        public DateTime StartDate { get; set; }

        [Required]
        [Display(Name = "結束日期")]
        public DateTime EndDate { get; set; }

        [MaxLength(100)]
        [Display(Name = "代理原因")]
        public string Reason { get; set; }

        [Display(Name = "趟數")]
        public int TripCount { get; set; }

        [Display(Name = "公里數")]
        public int DistanceKm { get; set; }

        [Display(Name = "建立時間")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
