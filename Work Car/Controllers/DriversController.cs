using Cars.Data;
using Cars.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Cars.Controllers
{
    [Authorize]
    [Route("Drivers")]

    public class DriversController : Controller
    {
        private readonly ApplicationDbContext _db;
        public DriversController(ApplicationDbContext db) => _db = db;

        // === API for Vue list ===
        [HttpGet("Records")]
        public async Task<ActionResult<IEnumerable<Driver>>> Records()
        {
            var drivers = await _db.Drivers
                .AsNoTracking()
                .OrderBy(d => d.DriverName)
                .ToListAsync();
            return Ok(drivers);
        }

        // === Pages ===
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var list = await _db.Drivers
                .AsNoTracking()
                .OrderBy(d => d.DriverName)
                .ToListAsync();
            return View(list);
        }

        [HttpGet("Details/{id}")]
        public async Task<IActionResult> DetailsApi(int id)
        {
            var driver = await _db.Drivers
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.DriverId == id);

            if (driver == null) return NotFound();

            return Json(driver);
        }


        [HttpGet("Create")]
        public IActionResult Create() => View();

        // POST: /Drivers/Create
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("DriverName,NationalId,BirthDate,HouseholdAddress,ContactAddress,Phone,Mobile,EmergencyContactName,EmergencyContactPhone")] Driver input)
        {
            if (!ModelState.IsValid)
            {
                return View(input);
            }

            // 例：若需要檢查身分證是否重複
            if (!string.IsNullOrWhiteSpace(input.NationalId))
            {
                var exists = await _db.Drivers.AnyAsync(x => x.NationalId == input.NationalId);
                if (exists)
                {
                    ModelState.AddModelError(nameof(input.NationalId), "此身分證字號已存在。");
                    return View(input);
                }
            }

            _db.Drivers.Add(input);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("Edit/{id:int}")]
        public async Task<IActionResult> Edit(int id)
        {
            var d = await _db.Drivers.FindAsync(id);
            if (d == null) return NotFound();
            return View(d);
        }

        // POST: /Drivers/Edit/5
        [HttpPost("Edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("DriverId,DriverName,NationalId,BirthDate,HouseholdAddress,ContactAddress,Phone,Mobile,EmergencyContactName,EmergencyContactPhone")] Driver input)
        {
            if (id != input.DriverId) return BadRequest();

            if (!ModelState.IsValid)
            {
                return View(input);
            }

            var entity = await _db.Drivers.FirstOrDefaultAsync(x => x.DriverId == id);
            if (entity == null) return NotFound();

            // （如需檢查 NationalId 是否與其他人重複）
            if (!string.IsNullOrWhiteSpace(input.NationalId))
            {
                var exists = await _db.Drivers
                    .AnyAsync(x => x.NationalId == input.NationalId && x.DriverId != id);
                if (exists)
                {
                    ModelState.AddModelError(nameof(input.NationalId), "此身分證字號已存在於其他駕駛。");
                    return View(input);
                }
            }

            // 僅更新允許的欄位（避免 Overposting）
            entity.DriverName = input.DriverName;
            entity.NationalId = input.NationalId;
            entity.BirthDate = input.BirthDate;
            entity.HouseholdAddress = input.HouseholdAddress;
            entity.ContactAddress = input.ContactAddress;
            entity.Phone = input.Phone;
            entity.Mobile = input.Mobile;
            entity.EmergencyContactName = input.EmergencyContactName;
            entity.EmergencyContactPhone = input.EmergencyContactPhone;

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        // 司機自己的班表頁
        [Authorize(Roles = "Driver")]
        [HttpGet("MySchedule")] 
        public IActionResult MySchedule() => View();

        [Authorize(Roles = "Driver")]
        [HttpGet("MySchedule/Events")]
        public async Task<IActionResult> MyScheduleEvents(DateTime? start, DateTime? end)
        {
            var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(uidStr, out var userId))
                return Forbid();

            // 從 Drivers 表找到對應的 DriverId
            var myDriverId = await _db.Drivers
                .AsNoTracking()
                .Where(d => d.UserId == userId)       // ⚠️ 這裡用 Driver.UserId
                .Select(d => (int?)d.DriverId)
                .FirstOrDefaultAsync();

            if (myDriverId == null || myDriverId == 0)
                return Forbid(); // 這個使用者沒有綁定司機

            // 查 Schedules 表，只抓自己的班表
            var q = _db.Schedules
                .AsNoTracking()
                .Where(s => s.DriverId == myDriverId.Value);

            if (start.HasValue) q = q.Where(s => s.WorkDate >= start.Value);
            if (end.HasValue) q = q.Where(s => s.WorkDate <= end.Value);

            var events = await q
                .OrderBy(s => s.WorkDate)
                .Select(s => new {
                    id = s.ScheduleId,
                    title = s.Shift,          // 直接顯示班別 (早班/午班…)
                    start = s.WorkDate,       // FullCalendar 會自動轉 ISO
                    end = s.WorkDate,       // 如果 Shift 沒有結束時間，就先用同一天
                    extendedProps = new
                    {
                        driverId = s.DriverId
                    }
                })
                .ToListAsync();

            return Ok(events);
        }


    }
}
