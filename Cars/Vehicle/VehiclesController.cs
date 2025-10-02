using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cars.Vehicle
{
    [Authorize]

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
   

        [HttpGet("/Fuel/Cards")]
        public IActionResult Cards(int? year, int? month)
        {
            var now = DateTime.Now;
            ViewBag.Year = year ?? now.Year;
            ViewBag.Month = month ?? now.Month;
            return View("FuelCardStats"); 
        }
        public IActionResult Statistics()
        {
            return View();
        }
        
    }
}
