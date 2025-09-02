using Cars.Data;
using Cars.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Cars.Controllers
{
    [Authorize]
    [Route("Agents")]
    [Authorize(Roles = "Admin")]
    public class AgentsController : Controller
    {
        private readonly ApplicationDbContext _db;

        public AgentsController(ApplicationDbContext db)
        {
            _db = db;
        }

        // 頁面
        [HttpGet("")]
        public IActionResult Index()
        {
            return View();
        }

        // === 提供代理紀錄 API ===
        [HttpGet("Records")]
        public async Task<IActionResult> Records()
        {
            var q = await _db.DriverDelegations
                .AsNoTracking()
                .Include(d => d.Agent)        // 關聯 DriverAgents
                .Include(d => d.Principal)    // 關聯 Drivers
                .OrderByDescending(d => d.StartDate)
                .Select(d => new
                {
                    id = d.DelegationId,
                    agentName = d.Agent.AgentName,     // 改用 DriverAgents.AgentName
                    principalName = d.Principal != null ? d.Principal.DriverName : "",
                    reason = d.Reason,
                    period = ToRocPeriod(d.StartDate, d.EndDate),
                    tripCount = d.TripCount,
                    distanceKm = d.DistanceKm
                })
                .ToListAsync();

            return Json(q);
        }

        // === 提供代理人員基本資料 API ===
        [HttpGet("Profiles")]
        public async Task<IActionResult> Profiles()
        {
            var list = await _db.DriverAgents
                .AsNoTracking()
                .OrderBy(a => a.AgentName)
                .ToListAsync();

            var result = list.Select(a => new
            {
                id = a.AgentId,
                name = a.AgentName,
                nationalId = a.NationalId,
                birthRoc = a.BirthDate.HasValue ? ToRocDate(a.BirthDate.Value) : "",
                household = a.HouseholdAddress,
                contact = a.ContactAddress,
                phone = a.Phone,
                mobile = a.Mobile,
                emergency = string.Join(" ",
                    new[] { a.EmergencyContactName, a.EmergencyContactPhone }
                    .Where(s => !string.IsNullOrWhiteSpace(s)))
            });

            return Json(result);
        }

        // === 小工具 ===
        private static string ToRocDate(DateTime dt)
            => $"{dt.Year - 1911}/{dt.Month:00}/{dt.Day:00}";

        private static string ToRocPeriod(DateTime start, DateTime end)
        {
            var rocY = start.Year - 1911;
            var s = string.Format("{0}/{1:00}/{2:00}", rocY, start.Month, start.Day);

            string e;
            if (start.Year == end.Year)
            {
                e = string.Format("{0:00}/{1:00}", end.Month, end.Day);
            }
            else
            {
                var rocY2 = end.Year - 1911;
                e = string.Format("{0}/{1:00}/{2:00}", rocY2, end.Month, end.Day);
            }
            return s + "-" + e;
        }
    }
}
