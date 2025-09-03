using System.Linq;
using System.Threading.Tasks;
using Cars.Data;
using Cars.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cars.Controllers
{
    [Authorize]
    [Authorize(Roles = "Admin")]
    public class InspectionsController : Controller
    {
        private readonly ApplicationDbContext _db;
        public InspectionsController(ApplicationDbContext db) { _db = db; }

        // GET: /Inspections
        public async Task<IActionResult> Index()
        {
            var list = await _db.VehicleInspections
                .Include(x => x.Vehicle)
                .OrderByDescending(x => x.InspectionDate)
                .ToListAsync();
            return View(list);
        }

        // GET: /Inspections/Create
        public async Task<IActionResult> Create()
        {
            await LoadVehicles();
            return View(new VehicleInspection { Result = "合格" });
        }

        // POST: /Inspections/Create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(VehicleInspection m)
        {
            if (!ModelState.IsValid)
            {
                await LoadVehicles();
                return View(m);
            }
            _db.VehicleInspections.Add(m);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: /Inspections/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var m = await _db.VehicleInspections.FindAsync(id);
            if (m == null) return NotFound();
            await LoadVehicles();
            return View(m);
        }

        // POST: /Inspections/Edit/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, VehicleInspection m)
        {
            if (id != m.InspectionId) return NotFound();
            if (!ModelState.IsValid)
            {
                await LoadVehicles();
                return View(m);
            }
            _db.Entry(m).State = EntityState.Modified;
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: /Inspections/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var m = await _db.VehicleInspections
                .Include(x => x.Vehicle)
                .FirstOrDefaultAsync(x => x.InspectionId == id);
            if (m == null) return NotFound();
            return View(m);
        }

        // POST: /Inspections/Delete/5
        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var m = await _db.VehicleInspections.FindAsync(id);
            if (m != null)
            {
                _db.VehicleInspections.Remove(m);
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
