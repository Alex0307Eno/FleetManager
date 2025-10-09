using System;

namespace Cars.Models
{
    public class VehicleLocationLog
    {
        public int Id { get; set; }
        public int VehicleId { get; set; }

        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double? Speed { get; set; }
        public double? Heading { get; set; }

        public DateTime GpsTime { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // optional: 關聯到 Vehicle
        public Vehicle Vehicle { get; set; }
    }
}
