namespace Cars.Shared.Dtos.CarApplications
{
    public record AssignDto
    {
        public int? DriverId { get; set; }
        public int? VehicleId { get; set; }
    }
}
