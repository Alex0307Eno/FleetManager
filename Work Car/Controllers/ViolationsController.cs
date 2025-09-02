using System.Linq;
using System.Threading.Tasks;
using Cars.Data;
using Cars.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cars.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ViolationsController : Controller
    {
        private readonly ApplicationDbContext _db;
        public ViolationsController(ApplicationDbContext db) { _db = db; }

        public async Task<IActionResult> Index()
        {
            var list = await _db.TrafficViolations
                .Include(x => x.Vehicle)
                .OrderByDescending(x => x.ViolationDate)
                .ToListAsync();
            return View(list);
        }

        public async Task<IActionResult> Create()
        {
            await LoadVehicles();
            return View(new TrafficViolation { Status = "未繳" });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TrafficViolation m)
        {
            if (!ModelState.IsValid)
            {
                await LoadVehicles();
                return View(m);
            }
            _db.TrafficViolations.Add(m);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var m = await _db.TrafficViolations.FindAsync(id);
            if (m == null) return NotFound();
            await LoadVehicles();
            return View(m);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TrafficViolation m)
        {
            if (id != m.ViolationId) return NotFound();
            if (!ModelState.IsValid)
            {
                await LoadVehicles();
                return View(m);
            }
            _db.Entry(m).State = EntityState.Modified;
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            var m = await _db.TrafficViolations
                .Include(x => x.Vehicle)
                .FirstOrDefaultAsync(x => x.ViolationId == id);
            if (m == null) return NotFound();
            return View(m);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var m = await _db.TrafficViolations.FindAsync(id);
            if (m != null)
            {
                _db.TrafficViolations.Remove(m);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task LoadVehicles()
        {
            var vehicles = await _db.Vehicles
                .AsNoTracking()
                .OrderBy(v => v.PlateNo)
                .Select(v => new { v.VehicleId, v.PlateNo })
                .ToListAsync();
            ViewBag.VehicleOptions = vehicles;
        }
    }
}
