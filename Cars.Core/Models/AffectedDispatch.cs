using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cars.Models
{
    public class AffectedDispatch
    {
        [Key]
        public int AffectedId { get; set; }

        [ForeignKey(nameof(Dispatch))]
        public int DispatchId { get; set; }

        [ForeignKey(nameof(Leave))]
        public int LeaveId { get; set; }

        [Required]
        public int DriverId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsResolved { get; set; } = false;

        public DateTime? ResolvedAt { get; set; }

        // --- 導覽屬性 ---
        public virtual Dispatch Dispatch { get; set; }

        public virtual Leave Leave { get; set; }
    }
}
