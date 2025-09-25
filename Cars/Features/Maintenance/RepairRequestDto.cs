namespace Cars.Features.Maintenance
{
    public sealed class RepairRequestDto
    {
        public int VehicleId { get; set; }
        public string? PlateNo { get; set; }
        public DateTime Date { get; set; }
        public string? Place { get; set; }
        public string Issue { get; set; } = "";
        public decimal? CostEstimate { get; set; }
        public string? Vendor { get; set; }
        public string? Note { get; set; }
    }
}
