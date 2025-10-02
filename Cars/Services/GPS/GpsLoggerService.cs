using Cars.Models;
using Cars.Data;
namespace Cars.Services.GPS
{

    public class GpsLoggerService
    {
        private readonly ApplicationDbContext _db;
        private readonly IGpsProvider _gps;

        public GpsLoggerService(ApplicationDbContext db, IGpsProvider gps)
        {
            _db = db;
            _gps = gps;
        }

        public async Task LogAsync(int vehicleId)
        {
            var loc = await _gps.GetLocationAsync(vehicleId);

            var entity = new VehicleLocationLog
            {
                VehicleId = loc.VehicleId,
                Latitude = loc.Latitude,
                Longitude = loc.Longitude,
                Speed = loc.Speed,
                Heading = loc.Heading,
                GpsTime = loc.GpsTime
            };

            _db.VehicleLocationLogs.Add(entity);
            await _db.SaveChangesAsync();
        }
    }

}
