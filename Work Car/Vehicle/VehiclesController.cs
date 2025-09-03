using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cars.Vehicle
{
    public class VehiclesController : Controller
    {
        [Authorize]
        public IActionResult Maintenance()
        {
            return View();
        }
        [Authorize]
        public IActionResult FuelStats()
        {
            return View();
        }
        [Authorize]
        public IActionResult Statistics()
        {
            return View();
        }
        [Authorize]
        public IActionResult FuelStatsChart()
        {
            return View();
        }
    }
}
