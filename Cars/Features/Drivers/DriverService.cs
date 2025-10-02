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

            // 1. 查出已經在該時段有派工的 DriverIds
            var busyDrivers = await _db.CarApplications
                .Where(d => d.UseStart < useEnd && d.UseEnd > useStart)
                .Select(d => d.DriverId)
                .ToListAsync();

            // 2. 查出每個駕駛最新完成的派工
            var lastTrips = await _db.Dispatches
                .Where(d => d.EndTime != null)
                .GroupBy(d => d.DriverId)
                .Select(g => new {
                    DriverId = g.Key,
                    LastEndTime = g.Max(x => x.EndTime)
                })
                .ToListAsync();

            // 3. 找出休息不足 1 小時的駕駛
            var restNotEnough = lastTrips
                .Where(t => useStart < t.LastEndTime.Value.AddHours(1))
                .Select(t => t.DriverId)
                .ToList();

            // 合併要排除的駕駛
            var excludedDrivers = busyDrivers.Concat(restNotEnough).Distinct().ToList();

            // 4. 全部 Drivers - 排除清單
            var drivers = await _db.Drivers
                .Where(d => !excludedDrivers.Contains(d.DriverId))
                .Select(d => new {
                    d.DriverId,
                    d.DriverName,
                    IsAgent = false
                })
                .ToListAsync();

            // 5. 當日有效的代理人
            var agents = await _db.DriverDelegations
                .Include(d => d.Agent)
                .Where(d => d.StartDate.Date <= today && today <= d.EndDate.Date &&
                            !excludedDrivers.Contains(d.AgentDriverId))
                .Select(d => new {
                    DriverId = d.AgentDriverId,
                    d.Agent.DriverName,
                    IsAgent = true
                })
                .ToListAsync();

            // 6. 合併 + 去重
            var all = drivers
                .Concat(agents)
                .GroupBy(x => x.DriverId)
                .Select(g => g.First())
                .ToList();

            return all.Cast<object>().ToList();
        }
    }

}
