using Microsoft.AspNetCore.Mvc;

namespace Cars.Vehicle
{
    public class VehicleController : Controller
    {
        public IActionResult Maintenance()
        {
            return View();
        }
        public IActionResult FuelStats()
        {
            return View();
        }
    }
}
