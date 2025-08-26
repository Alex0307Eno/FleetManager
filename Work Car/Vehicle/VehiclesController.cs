using Microsoft.AspNetCore.Mvc;

namespace Cars.Vehicle
{
    public class VehiclesController : Controller
    {
        public IActionResult Maintenance()
        {
            return View();
        }
        public IActionResult FuelStats()
        {
            return View();
        }
        public IActionResult Statistics()
        {
            return View();
        }
        public IActionResult FuelStatsChart()
        {
            return View();
        }
    }
}
