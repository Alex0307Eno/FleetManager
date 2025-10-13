using System;

namespace Cars.Shared.Dtos.Maintenance
{
    public class VehicleUpdateDto
    {
        public string? Source { get; set; }
        public string? ApprovalNo { get; set; }
        public DateTime? PurchaseDate { get; set; }
        public decimal? Value { get; set; }
        public int? Odometer { get; set; }
        public DateTime? LicenseDate { get; set; }
        public DateTime? StartUseDate { get; set; }
        public DateTime? InspectionDate { get; set; }
        public int? EngineCC { get; set; }
        public string? EngineNo { get; set; }
        public string? Brand { get; set; }
        public int? Year { get; set; }
        public string? Model { get; set; }
        public string? Type { get; set; }
        public DateTime? RetiredDate { get; set; }
    }
}
