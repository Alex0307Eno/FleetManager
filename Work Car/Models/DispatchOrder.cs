using Microsoft.EntityFrameworkCore;

namespace Cars.Models
{
    [Keyless] // 沒有主鍵的 View
    public class DispatchOrder
    {
        public int DispatchId { get; set; }   // 即使不是唯一，也可以放
        public int? DriverId { get; set; }
        public string? DriverName { get; set; }
        public int? VehicleId { get; set; }
        public string? VehiclePlate { get; set; }
        public string? DispatchStatus { get; set; }
    }
}
