using Cars.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cars.ApiControllers
{
    [ApiController]
    [Route("api/agents")]
    [Authorize(Roles = "Admin")]
    public class AgentsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public AgentsController(ApplicationDbContext db)
        {
            _db = db;
        }

        // === 代理紀錄 (JSON) ===
        [HttpGet("records")]
        public async Task<IActionResult> Records()
        {
            var q = await _db.DriverDelegations
                .AsNoTracking()
                .Include(d => d.Agent)
                .Include(d => d.Principal)
                .OrderByDescending(d => d.StartDate)
                .Select(d => new
                {
                    id = d.DelegationId,
                    agentId = d.AgentDriverId,
                    agentName = d.Agent != null ? d.Agent.DriverName : "",
                    principalId = d.PrincipalDriverId,
                    principalName = d.Principal != null ? d.Principal.DriverName : "",
                    reason = d.Reason,
                    period = ToRocPeriod(d.StartDate, d.EndDate),
                    tripCount = _db.v_DispatchOrders
                        .Where(v => v.DriverId == d.AgentDriverId
                                 && v.UseStart >= d.StartDate.Date
                                 && v.UseStart < d.EndDate.Date.AddDays(1))
                        .Count(),
                    distanceKm = _db.v_DispatchOrders
                        .Where(v => v.DriverId == d.AgentDriverId
                                 && v.UseStart >= d.StartDate.Date
                                 && v.UseStart < d.EndDate.Date.AddDays(1))
                        .Sum(v => v.TripDistance ?? 0m)
                })
                .ToListAsync();

            return Ok(q);
        }

        // === 代理人基本資料 (JSON) ===
        [HttpGet("profiles")]
        public async Task<IActionResult> Profiles()
        {
            var list = await _db.Drivers
                .AsNoTracking()
                .Where(d => d.IsAgent)
                .OrderBy(d => d.DriverName)
                .ToListAsync();

            var result = list.Select(d => new
            {
                driverId = d.DriverId,
                name = d.DriverName,
                nationalId = d.NationalId,
                birthRoc = d.BirthDate.HasValue ? ToRocDate(d.BirthDate.Value) : "",
                household = d.HouseholdAddress,
                contact = d.ContactAddress,
                phone = d.Phone,
                mobile = d.Mobile,
                emergency = d.EmergencyContactName,
                emergencyPhone = d.EmergencyContactPhone,
            });

            return Ok(result);
        }

        // 工具
        private static string ToRocDate(DateTime dt)
            => $"{dt.Year - 1911}/{dt.Month:00}/{dt.Day:00}";

        private static string ToRocPeriod(DateTime start, DateTime end)
        {
            var rocY = start.Year - 1911;
            var s = $"{rocY}/{start.Month:00}/{start.Day:00}";

            string e = (start.Year == end.Year)
                ? $"{end.Month:00}/{end.Day:00}"
                : $"{end.Year - 1911}/{end.Month:00}/{end.Day:00}";

            return s + "-" + e;
        }
    }
}
