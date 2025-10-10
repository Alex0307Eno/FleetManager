using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Cars.Services.GPS
{
    public class FakeGpsProvider : IGpsProvider
    {
        private static readonly Dictionary<int, (double lat, double lng)> _fakePositions =
            new Dictionary<int, (double lat, double lng)>
            {
                { 1, (lat: 25.0478, lng: 121.5170) },  // 台北車站
                { 2, (lat: 24.1477, lng: 120.6736) },  // 台中
                { 3, (lat: 22.6273, lng: 120.3014) }   // 高雄
            };

        private static readonly Random _rand = new Random();

        public async Task<VehicleLocation> GetLocationAsync(int vehicleId)
        {
            await Task.Delay(100); // 模擬網路延遲

            // 每台車有固定起點（台北、高雄、台中）
            var basePos = _fakePositions.ContainsKey(vehicleId)
                ? _fakePositions[vehicleId]
                : (25.0478, 121.5170); // 預設台北

            // ===  加上隨機飄移（讓車子看起來會動） ===
            double jitterLat = basePos.Item1 + (_rand.NextDouble() - 0.5) / 1000; // ±0.0005 經緯度 ≈ ±50m
            double jitterLng = basePos.Item2 + (_rand.NextDouble() - 0.5) / 1000;
            double fakeSpeed = 30 + _rand.NextDouble() * 20; // 30~50 km/h 隨機速度

            return new VehicleLocation
            {
                VehicleId = vehicleId,
                Latitude = jitterLat,
                Longitude = jitterLng,
                Speed = fakeSpeed,
                Heading = _rand.Next(0, 360),
                GpsTime = DateTime.UtcNow
            };
        }

    }
}

