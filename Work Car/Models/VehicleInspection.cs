using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cars.Models
{
    public class VehicleInspection
    {
        [Key]
        public int InspectionId { get; set; }

        [Required]
        public int VehicleId { get; set; }          // FK → Vehicles.VehicleId

        [Required]
        public DateTime InspectionDate { get; set; } // 驗車日期

        [MaxLength(80)]
        public string Station { get; set; }          // 驗車站/監理站

        [Required, MaxLength(20)]
        public string Result { get; set; }           // 合格/不合格

        public DateTime? NextDueDate { get; set; }   // 下次到期日

        public int? OdometerKm { get; set; }         // 里程

        [MaxLength(500)]
        public string Notes { get; set; }            // 備註

        // 導覽
        [ForeignKey("VehicleId")]
        public virtual Vehicle Vehicle { get; set; }
    }
}
