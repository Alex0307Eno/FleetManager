namespace Cars.Models
{
    public class Dispatch
    {
        public int DispatchId { get; set; }
        public int ApplyId { get; set; }
        public int? DriverId { get; set; }
        public string? DriverName { get; set; }
        public string? VehiclePlate { get; set; }

        public int? VehicleId { get; set; }
        public string DispatchStatus { get; set; } = "待派車";

        public virtual CarApply? Application { get; set; }
        public Vehicle? Car { get; set; }
        public Driver? Driver { get; set; }
    }
}
