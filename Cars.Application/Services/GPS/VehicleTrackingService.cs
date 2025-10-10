namespace Cars.Services.GPS
{
    public class VehicleTrackingService
    {
        private readonly IGpsProvider _gps;

        public VehicleTrackingService(IGpsProvider gps)
        {
            _gps = gps;
        }

        public async Task TrackAsync(int vehicleId)
        {
            var location = await _gps.GetLocationAsync(vehicleId);
            var speed = location.Speed.HasValue ? location.Speed.Value : 0.0;
            Console.WriteLine(
                "車輛 {0} 在 {1:F6},{2:F6} 速度 {3:F1} km/h",
                location.VehicleId, location.Latitude, location.Longitude, speed);
        }
    }

}
