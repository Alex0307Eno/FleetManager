using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Cars.Services.GPS
{


    public class HttpGpsProvider : IGpsProvider
    {
        private readonly HttpClient _http;

        public HttpGpsProvider(HttpClient http)
        {
            _http = http;
        }

        public async Task<VehicleLocation> GetLocationAsync(int vehicleId)
        {
            var res = await _http.GetStringAsync("?vehicleId=" + vehicleId);
            var dto = JsonSerializer.Deserialize<HttpGpsDto>(res, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (dto == null)
                throw new Exception("遠端 GPS 回傳為空或格式不正確");

            return new VehicleLocation
            {
                VehicleId = vehicleId,
                Latitude = dto.Lat,
                Longitude = dto.Lng,
                Speed = dto.Speed,
                Heading = dto.Heading,
                GpsTime = dto.Timestamp.HasValue ? dto.Timestamp.Value : DateTime.UtcNow
            };
        }

        private class HttpGpsDto
        {
            public double Lat { get; set; }
            public double Lng { get; set; }
            public double? Speed { get; set; }
            public double? Heading { get; set; }
            public DateTime? Timestamp { get; set; }
        }
    }
}
