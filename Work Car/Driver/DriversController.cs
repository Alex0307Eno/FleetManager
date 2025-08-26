using Microsoft.AspNetCore.Mvc;

namespace Cars.driver
{
    public class DriversController : Controller
    {
        public IActionResult Schedule()
        {
            return View();
        }
        public IActionResult Index()
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

        public IActionResult Agent()
        {
            return View();
        }
    }
}
