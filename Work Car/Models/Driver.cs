namespace Cars.Models
{
    public class Driver
    {
        public int DriverId { get; set; }
        public string? DriverName { get; set; }
        public string? Dept { get; set; }
        public string? JobTitle { get; set; }
        public string? Ext { get; set; }
        public string? Email { get; set; }

        // 🔗 關聯
        public ICollection<Dispatch>? CarApplications { get; set; }
        public ICollection<Dispatch>? DispatchOrders { get; set; }
    }
}
