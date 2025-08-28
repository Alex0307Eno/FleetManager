namespace Cars.Models
{
    public class Vehicle
    {
        public int VehicleId { get; set; }
        public string? PlateNo { get; set; }
        public string? Brand { get; set; }
        public string? Model { get; set; }
        public int Capacity { get; set; }
        public string? Status { get; set; }

        public string? Type { get; set; }
        public DateTime? PurchaseDate { get; set; }
        public decimal? Value { get; set; }
        public DateTime? LicenseDate { get; set; }
        public DateTime? StartUseDate { get; set; }
        public DateTime? InspectionDate { get; set; }
        public int? EngineCC { get; set; }
        public string? EngineNo { get; set; }
        public int? Year { get; set; }
        public string? Source { get; set; }
        public string? ApprovalNo { get; set; }
        public bool Retired { get; set; } = false;



        // 🔗 關聯
        public ICollection<Dispatch>? DispatchOrders { get; set; }
    }
}
