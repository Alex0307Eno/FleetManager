using System;

namespace Cars.Shared.Dtos.Maintenance
{
    public sealed class ViolationDto
    {
        public int VehicleId { get; set; }
        public DateTime ViolationDate { get; set; }
        public string? Location { get; set; }
        public string? Category { get; set; }
        public int? Points { get; set; }
        public int? FineAmount { get; set; }
        public string? Status { get; set; }  // 未繳/已繳/申訴中
        public DateTime? DueDate { get; set; }
        public DateTime? PaidDate { get; set; }
        public string? Notes { get; set; }
    }
}
