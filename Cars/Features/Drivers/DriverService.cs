using Cars.Data;
using Microsoft.EntityFrameworkCore;

namespace Cars.Features.Drivers
{
    public class DriverService
    {
        private readonly ApplicationDbContext _db;
        public DriverService(ApplicationDbContext db) => _db = db;

        public async Task<List<object>> GetAvailableDriversAsync(DateTime useStart, DateTime useEnd)
        {
            var today = DateTime.Today;

            // 查出已經在該時段有派工的 DriverIds
            var busyDrivers = await _db.Dispatches
                .Where(d => d.StartTime < useEnd && d.EndTime > useStart)
                .Select(d => d.DriverId)
                .ToListAsync();

            // 1. 當日正常出勤的司機
            var drivers = await _db.Drivers
                .Where(d => _db.Schedules.Any(s =>
                    s.DriverId == d.DriverId &&
                    s.WorkDate == today &&
                    s.IsPresent == true) &&
                    !busyDrivers.Contains(d.DriverId))
                .Select(d => new {
                    d.DriverId,
                    d.DriverName,
                    IsAgent = false
                })
                .ToListAsync();

            // 2. 當日有效的代理人
            var agents = await _db.DriverDelegations
                .Include(d => d.Agent)
                .Where(d => d.StartDate.Date <= today && today <= d.EndDate.Date &&
                            !busyDrivers.Contains(d.AgentDriverId))
                .Select(d => new {
                    DriverId = d.AgentDriverId,
                    d.Agent.DriverName,
                    IsAgent = true
                })
                .ToListAsync();

            // 3. 合併 + 去重
            var all = drivers
                .Concat(agents)
                .GroupBy(x => x.DriverId)
                .Select(g => g.First())
                .ToList();

            return all.Cast<object>().ToList();
        }
    }

}
