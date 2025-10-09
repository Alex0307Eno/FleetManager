namespace Cars.Features.Maintenance
{
    public sealed class MaintainanceCreateDto
    {
        public int VehicleId { get; set; }
        public string? VehiclePlate { get; set; }
        public DateTime Date { get; set; }
        public int? Odometer { get; set; }
        public string Item { get; set; } = "";
        public string? Unit { get; set; }
        public decimal? Qty { get; set; }
        public decimal? Amount { get; set; }
        public string? Vendor { get; set; }
        public string? Note { get; set; }

        public DateTime? NextDueDate { get; set; }
    }
}
