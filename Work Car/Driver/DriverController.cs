using Microsoft.AspNetCore.Mvc;

namespace Cars.driver
{
    public class DriverController : Controller
    {
        public IActionResult Schedule()
        {
            return View();
        }
    }
}
