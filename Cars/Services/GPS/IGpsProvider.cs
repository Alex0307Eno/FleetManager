namespace Cars.Services.GPS
{
    public interface IGpsProvider
    {
        Task<VehicleLocation> GetLocationAsync(int vehicleId);
    }

    public class VehicleLocation
    {
        public int VehicleId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double? Speed { get; set; }
        public double? Heading { get; set; }
        public DateTime GpsTime { get; set; }
    }
}
