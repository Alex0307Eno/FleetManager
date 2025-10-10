using Cars.Data;
using Cars.Models;
using Cars.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cars.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("Agents")]
    public class AgentsController : Controller
    {
        private readonly ApplicationDbContext _db;

        public AgentsController(ApplicationDbContext db)
        {
            _db = db;
        }

        // === MVC: 頁面 ===
        [HttpGet("Index")]
        public IActionResult Index() 
        {
            return View();
        }


        [HttpGet("Create")]
        public IActionResult Create()
        {
            return View(new Driver { IsAgent = true });
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Driver agent)
        {
            if (ModelState.IsValid)
            {
                agent.IsAgent = true;
                _db.Add(agent);
                var (ok,err) = await _db.TrySaveChangesAsync(this);
                if (!ok) return err!;
                return RedirectToAction("Index");
            }
            return View(agent);
        }

        [HttpGet("Edit/{id:int}")]
        public async Task<IActionResult> Edit(int id)
        {
            var agent = await _db.Drivers.FirstOrDefaultAsync(d => d.DriverId == id && d.IsAgent);
            if (agent == null) return NotFound();
            return View(agent);
        }

        [HttpPost("Edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Driver agent)
        {
            if (id != agent.DriverId) return BadRequest();

            if (ModelState.IsValid)
            {
                agent.IsAgent = true;
                _db.Update(agent);
                var (ok, err) = await _db.TrySaveChangesAsync(this);
                if (!ok) return err!;
                return RedirectToAction("Index");
            }
            return View(agent);
        }
    }
}
