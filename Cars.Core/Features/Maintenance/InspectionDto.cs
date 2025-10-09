namespace Cars.Features.Maintenance
{
    public sealed class InspectionDto
    {
        public int VehicleId { get; set; }
        public DateTime InspectionDate { get; set; }
        public string? Station { get; set; }
        public string Result { get; set; } = "合格";
        public DateTime? NextDueDate { get; set; }
        public int? OdometerKm { get; set; }
        public string? Notes { get; set; }
    }
}
