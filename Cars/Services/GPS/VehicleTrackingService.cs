namespace Cars.Services.GPS
{
    public class VehicleTrackingService
    {
        private readonly HttpGpsProvider _gps;

        public VehicleTrackingService(HttpGpsProvider gps)
        {
            _gps = gps;
        }

        public async Task TrackAsync()
        {
            var location = await _gps.GetLocationAsync("ABC123");
            Console.WriteLine($"車輛 {location.DeviceId} 在 {location.Lat},{location.Lng} 速度 {location.Speed} km/h");
        }
    }

}
