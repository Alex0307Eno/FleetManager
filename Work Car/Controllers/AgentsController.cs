using Cars.Data;
using Cars.Models;
using DocumentFormat.OpenXml.InkML;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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



        #region 代理紀錄
        // 代理紀錄 
        [HttpGet("Records")]
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

            return Json(q);
        }
        #endregion

        #region 代理人基本資料

        //代理人員基本資料
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
                emergency = string.Join(" ",
                    new[] { d.EmergencyContactName, d.EmergencyContactPhone }
                    .Where(s => !string.IsNullOrWhiteSpace(s)))
            });

            return Json(result);
        }
        #endregion




        #region 新增代理人


        public IActionResult Create()
        {
            return View(new Driver { IsAgent = true });
        }

        [HttpPost]
        [Route("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Driver agent)
        {
            if (ModelState.IsValid)
            {
                agent.IsAgent = true;
                _db.Add(agent);
                await _db.SaveChangesAsync();
                return RedirectToAction("Index");
            }
            return View(agent);
        }
        #endregion

        #region 編輯代理人

        [HttpGet("Edit/{id:int}")]

        public async Task<IActionResult> Edit(int id)
        {
            var agent = await _db.Drivers.FirstOrDefaultAsync(d => d.DriverId == id && d.IsAgent == true);
            if (agent == null) return NotFound();
            return View(agent);
        }

        [HttpPost]
        [HttpPost("Edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Driver agent)
        {
            if (id != agent.DriverId) return BadRequest();

            if (ModelState.IsValid)
            {
                try
                {
                    agent.IsAgent = true;
                    _db.Update(agent);
                    await _db.SaveChangesAsync();
                    return RedirectToAction("Index");
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_db.Drivers.Any(e => e.DriverId == id)) return NotFound();
                    throw;
                }
            }
            return View(agent);
        }
        #endregion

        #region 轉換工具
        //小工具 
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
        #endregion
    }
}
