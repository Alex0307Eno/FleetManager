using Microsoft.AspNetCore.Mvc;
using Cars.Data;
using Cars.Models;

namespace Cars.Controllers
{
    public class CarApplicationController : Controller
    {
        private readonly ApplicationDbContext _context;
        public CarApplicationController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public IActionResult Create(CarApply model)
        {
            if (ModelState.IsValid)
            {
                _context.CarApplications.Add(model);
                _context.SaveChanges();
                return RedirectToAction("Index", "Home");
            }
            return View(model);
        }
    }
}
