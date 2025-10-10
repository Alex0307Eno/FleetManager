using Cars.Data;
using Cars.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Cars.Services.GPS
{
    public class VehicleLocationSimulator : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly Random _rand = new Random();

        public VehicleLocationSimulator(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var cities = new (string Name, double Lat, double Lng)[]
            {
                ("台北", 25.033964, 121.564468),
                ("台中", 24.147735, 120.673648),
                ("高雄", 22.627278, 120.301435),
                ("花蓮", 23.987158, 121.601571),
                ("台東", 22.761864, 121.131229)
            };

            var targets = new Dictionary<int, (double Lat, double Lng)>();
            var rand = new Random();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var vehicles = await db.Vehicles.AsNoTracking().ToListAsync(stoppingToken);
                        if (vehicles.Count == 0)
                        {
                            Console.WriteLine("⚠️ 無車輛資料，略過。");
                            await Task.Delay(5000, stoppingToken);
                            continue;
                        }

                        var latest = await db.VehicleLocationLogs
                            .GroupBy(x => x.VehicleId)
                            .Select(g => g.OrderByDescending(x => x.GpsTime).FirstOrDefault())
                            .ToListAsync(stoppingToken);

                        foreach (var v in vehicles)
                        {
                            var last = latest.FirstOrDefault(l => l.VehicleId == v.VehicleId);
                            double lat, lng;

                            if (last == null)
                            {
                                lat = 24.147735 + v.VehicleId * 0.001;
                                lng = 120.673648 + v.VehicleId * 0.001;
                            }
                            else
                            {
                                lat = last.Latitude;
                                lng = last.Longitude;
                            }

                            if (!targets.ContainsKey(v.VehicleId) ||
                                GetDistanceKm(lat, lng, targets[v.VehicleId].Lat, targets[v.VehicleId].Lng) < 2)
                            {
                                var newTarget = cities[rand.Next(cities.Length)];
                                targets[v.VehicleId] = (newTarget.Lat, newTarget.Lng);
                                Console.WriteLine($"🚗 車 {v.VehicleId} 目標改為 {newTarget.Name}");
                            }

                            var target = targets[v.VehicleId];
                            var stepLat = (target.Lat - lat) / 500;
                            var stepLng = (target.Lng - lng) / 500;

                            lat += stepLat + (rand.NextDouble() - 0.5) * 0.0003;
                            lng += stepLng + (rand.NextDouble() - 0.5) * 0.0003;

                            db.VehicleLocationLogs.Add(new VehicleLocationLog
                            {
                                VehicleId = v.VehicleId,
                                Latitude = lat,
                                Longitude = lng,
                                Speed = rand.Next(50, 90),
                                Heading = rand.Next(0, 360),
                                GpsTime = DateTime.Now,
                                CreatedAt = DateTime.UtcNow
                            });
                        }

                        await db.SaveChangesAsync(stoppingToken);
                    }

                    await Task.Delay(3000, stoppingToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ 模擬錯誤：{ex.Message}");
                    await Task.Delay(5000, stoppingToken);
                }
            }
        }

        private static double GetDistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371;
            double dLat = (lat2 - lat1) * Math.PI / 180;
            double dLon = (lon2 - lon1) * Math.PI / 180;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }
    }
}
