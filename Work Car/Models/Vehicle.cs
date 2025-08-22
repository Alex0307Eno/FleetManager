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

        // 🔗 關聯
        public ICollection<Dispatch>? DispatchOrders { get; set; }
    }
}
