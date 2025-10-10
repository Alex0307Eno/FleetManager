using Cars.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Cars.Services
{
    public class DriverService
    {
        private readonly ApplicationDbContext _db;
        public DriverService(ApplicationDbContext db) => _db = db;

        public async Task<List<object>> GetAvailableDriversAsync(DateTime useStart, DateTime useEnd)
        {
            var today = DateTime.Today;
            var oneHour = TimeSpan.FromHours(1);

            Console.WriteLine($"\n🔍 [DriverService] 查詢可用駕駛：{useStart:yyyy-MM-dd HH:mm} ~ {useEnd:yyyy-MM-dd HH:mm}");

            // === Step 1. 找出正在忙的駕駛 ===
            var busyDrivers = await _db.CarApplications
                .Where(d => d.UseStart < useEnd && d.UseEnd > useStart && d.DriverId != null)
                .Select(d => d.DriverId.Value)
                .Distinct()
                .ToListAsync();

            foreach (var id in busyDrivers)
                Console.WriteLine($"🚫 駕駛 {id}：正在執行派工中");

            // === Step 2. 找出今天結束任務、尚未休息滿一小時的駕駛 ===
            var now = DateTime.Now;

            var lastTrips = await (
                from dis in _db.Dispatches
                join app in _db.CarApplications on dis.ApplyId equals app.ApplyId
                where dis.DriverId != null
                      && ((dis.EndTime ?? app.UseEnd) <= now) // ✅ 不看未來的
                group new { dis, app } by dis.DriverId into g
                select new
                {
                    DriverId = g.Key.Value,
                    LastEndTime = (DateTime?)g.Max(x => x.dis.EndTime ?? x.app.UseEnd)
                }
            ).ToListAsync();

            var restNotEnough = lastTrips
                .Where(t => t.LastEndTime != null && t.LastEndTime.Value.Add(oneHour) > useStart)
                .Select(t => t.DriverId)
                .ToList();


            foreach (var id in restNotEnough)
                Console.WriteLine($"😴 駕駛 {id}：休息未滿一小時");

            // === Step 3. 今日代理紀錄 ===
            var delegatedToday = await _db.DriverDelegations
                .Where(d => d.StartDate.Date <= today && (d.EndDate == null || today <= d.EndDate.Date))
                .Select(d => new { d.PrincipalDriverId, d.AgentDriverId })
                .ToListAsync();

            var delegatedPrincipalIds = delegatedToday.Select(d => d.PrincipalDriverId).Distinct().ToList();

            foreach (var id in delegatedPrincipalIds)
                Console.WriteLine($"👥 駕駛 {id}：已被代理，不可再指派");

            // === Step 4. 合併排除 ===
            var excludedDrivers = busyDrivers
                .Concat(restNotEnough)
                .Concat(delegatedPrincipalIds)
                .Distinct()
                .ToList();

            // === Step 5. 主駕 ===
            var drivers = await _db.Drivers
                .Where(d => !excludedDrivers.Contains(d.DriverId))
                .Select(d => new {
                    d.DriverId,
                    d.DriverName,
                    d.IsAgent
                })
                .ToListAsync();

            // === Step 6. 代理駕駛 ===
            var agentCandidates = await _db.DriverDelegations
                .Include(d => d.Agent)
                .Where(d => d.StartDate.Date <= today && (d.EndDate == null || today <= d.EndDate.Date))
                .Select(d => new { d.AgentDriverId, d.Agent.DriverName })
                .Distinct()
                .ToListAsync();

            var agentIds = agentCandidates.Select(a => a.AgentDriverId).ToList();

            var agentTrips = await _db.Dispatches
                .Where(d => d.DriverId != null && agentIds.Contains(d.DriverId.Value))
                .GroupBy(d => d.DriverId)
                .Select(g => new {
                    DriverId = g.Key.Value,
                    LastEndTime = (DateTime?)g.Max(x => x.EndTime)
                })
                .ToListAsync();

            var restAgents = agentTrips
                .Where(t => t.LastEndTime == null || t.LastEndTime.Value.Add(oneHour) <= useStart)
                .Select(t => t.DriverId)
                .Distinct()
                .ToList();

            var agents = agentCandidates
                .Where(a => restAgents.Contains(a.AgentDriverId))
                .Select(a => new {
                    DriverId = a.AgentDriverId,
                    DriverName = a.DriverName,
                    IsAgent = true
                })
                .ToList();

            foreach (var id in agentIds.Except(restAgents))
                Console.WriteLine($"🕓 代理駕駛 {id}：休息未滿一小時（暫不顯示）");

            // === Step 7. 合併 ===
            var all = drivers.Concat(agents)
                             .GroupBy(x => x.DriverId)
                             .Select(g => g.First())
                             .ToList();

            Console.WriteLine($"✅ 可用駕駛：{string.Join(", ", all.Select(a => $"{a.DriverId}-{a.DriverName}"))}");
            Console.WriteLine($"=== 共 {all.Count} 位可派駕駛 ===\n");

            return all.Cast<object>().ToList();
        }
    }
}
