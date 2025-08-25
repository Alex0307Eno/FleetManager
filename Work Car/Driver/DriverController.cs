using Microsoft.AspNetCore.Mvc;

namespace Cars.driver
{
    public class DriverController : Controller
    {
        public IActionResult Schedule()
        {
            return View();
        }
        public IActionResult Driver()
        {
            return View();
        }
        public IActionResult _DriverForm()
        {
            return View();
        }

        public IActionResult Create()
        {
            return View();
        }
        public IActionResult Edit()
        {
            return View();
        }
    }
}
