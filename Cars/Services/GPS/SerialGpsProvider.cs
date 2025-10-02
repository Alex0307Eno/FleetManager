using System.IO.Ports;

namespace Cars.Services.GPS
{
    public class SerialGpsProvider : IGpsProvider
    {
        private readonly string _portName;

        public SerialGpsProvider(string portName)
        {
            _portName = portName;
        }

        public async Task<VehicleLocation> GetLocationAsync(int vehicleId)
        {
            using (var port = new SerialPort(_portName, 9600))
            {
                port.Open();
                string line = port.ReadLine(); // 讀取一行 NMEA
                var parts = line.Split(',');

                double lat = ParseLat(parts[3], parts[4]);
                double lon = ParseLon(parts[5], parts[6]);

                return new VehicleLocation
                {
                    VehicleId = vehicleId,
                    Latitude = lat,
                    Longitude = lon,
                    Speed = double.TryParse(parts[7], out var s) ? s * 1.852 : null,
                    Heading = double.TryParse(parts[8], out var h) ? h : null,
                    GpsTime = DateTime.UtcNow
                };
            }
        }

        private double ParseLat(string value, string ns) =>
            double.Parse(value.Substring(0, 2)) + double.Parse(value.Substring(2)) / 60 * (ns == "S" ? -1 : 1);

        private double ParseLon(string value, string ew) =>
            double.Parse(value.Substring(0, 3)) + double.Parse(value.Substring(3)) / 60 * (ew == "W" ? -1 : 1);
    }
}
