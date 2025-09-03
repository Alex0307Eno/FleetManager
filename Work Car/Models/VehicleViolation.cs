using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cars.Models
{
    public class VehicleViolation
    {
        [Key]
        public int ViolationId { get; set; }

        [Required]
        public int VehicleId { get; set; }           // FK → Vehicles.VehicleId

        [Required]
        public DateTime ViolationDate { get; set; }  // 發生日期

        [MaxLength(120)]
        public string Location { get; set; }         // 地點

        [MaxLength(80)]
        public string Category { get; set; }         // 例如：超速、違規停車…

        public int? Points { get; set; }             // 記點

        public int? FineAmount { get; set; }         // 罰鍰(元)

        [MaxLength(20)]
        public string Status { get; set; }           // 未繳/已繳/申訴中

        public DateTime? DueDate { get; set; }       // 繳費期限
        public DateTime? PaidDate { get; set; }      // 繳費日期

        [MaxLength(500)]
        public string Notes { get; set; }            // 備註

        // 導覽
        [ForeignKey("VehicleId")]
        public virtual Vehicle Vehicle { get; set; }
    }
}
