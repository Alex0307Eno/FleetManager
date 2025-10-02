using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Cars.Services.GPS
{
    public class GpsLocation
    {
        public string DeviceId { get; set; }
        public double Lat { get; set; }
        public double Lng { get; set; }
        public double Speed { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class HttpGpsProvider
    {
        private readonly HttpClient _http;

        public HttpGpsProvider(HttpClient http)
        {
            _http = http;
        }

        public async Task<GpsLocation> GetLocationAsync(string deviceId)
        {
            var res = await _http.GetStringAsync($"https://gps-provider.com/api/location?deviceId={deviceId}");
            var gps = JsonSerializer.Deserialize<GpsLocation>(res, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return gps;
        }
    }
}
