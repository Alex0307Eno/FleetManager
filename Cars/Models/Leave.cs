using System;
using System.ComponentModel.DataAnnotations;

namespace Cars.Models
{
    public class Leave
    {
        [Key]
        public int LeaveId { get; set; }

        public int DriverId { get; set; }
        public Driver Driver { get; set; }

        public int? AgentDriverId { get; set; }
        public Driver AgentDriver { get; set; } 

        [Required]
        public string LeaveType { get; set; }

        [Required]
        public DateTime Start { get; set; }

        [Required]
        public DateTime End { get; set; }

        [Required]
        public string Reason { get; set; }

        public string Status { get; set; } = "待審核";

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
