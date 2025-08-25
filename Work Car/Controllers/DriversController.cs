using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Cars.Data;
using Cars.Models;

namespace Cars.Controllers
{
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

        [HttpGet("Details/{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            var d = await _db.Drivers.AsNoTracking()
                .FirstOrDefaultAsync(x => x.DriverId == id);
            if (d == null) return NotFound();
            return View(d);
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
    }
}
