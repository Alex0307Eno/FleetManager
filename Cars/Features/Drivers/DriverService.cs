using Cars.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Cars.Features.Drivers
{
    public class DriverService
    {
        private readonly ApplicationDbContext _db;
        public DriverService(ApplicationDbContext db) => _db = db;

        public async Task<List<object>> GetAvailableDriversAsync(DateTime useStart, DateTime useEnd)
        {
            var today = DateTime.Today;

            var busyDrivers = await _db.CarApplications
                .Where(d => d.UseStart < useEnd && d.UseEnd > useStart && d.DriverId != null)
                .Select(d => d.DriverId)
                .ToListAsync();

            var lastTrips = await _db.Dispatches
                .Where(d => d.EndTime != null)
                .GroupBy(d => d.DriverId)
                .Select(g => new {
                    DriverId = g.Key,
                    LastEndTime = g.Max(x => x.EndTime)
                })
                .ToListAsync();

            var restNotEnough = lastTrips
                .Where(t => t.LastEndTime.HasValue && useStart < t.LastEndTime.Value.AddHours(1))
                .Select(t => t.DriverId)
                .ToList();

            var excludedDrivers = busyDrivers.Concat(restNotEnough).Distinct().ToList();

            var drivers = await _db.Drivers
                .Where(d => !excludedDrivers.Contains(d.DriverId))
                .Select(d => new {
                    d.DriverId,
                    d.DriverName,
                    IsAgent = false
                })
                .ToListAsync();

            var agents = await _db.DriverDelegations
                .Include(d => d.Agent)
                .Where(d => d.StartDate.Date <= today &&
                            (d.EndDate == null || today <= d.EndDate.Date) &&
                            !excludedDrivers.Contains(d.AgentDriverId))
                .Select(d => new {
                    DriverId = d.AgentDriverId,
                    d.Agent.DriverName,
                    IsAgent = true
                })
                .ToListAsync();

            var all = drivers
                .Concat(agents)
                .GroupBy(x => x.DriverId)
                .Select(g => g.First())
                .ToList();

            return all.Cast<object>().ToList();
        }
    }

}
