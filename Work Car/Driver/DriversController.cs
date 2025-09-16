using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cars.driver
{
    public class DriversController : Controller
    {
        [Authorize]
        public IActionResult Schedule()
        {
            return View();
        }
        [Authorize]
        public IActionResult Index()
        {
            return View();
        }
        [Authorize]
        public IActionResult _DriverForm()
        {
            return View();
        }
        [Authorize]
        public IActionResult Create()
        {
            return View();
        }
        [Authorize]
        public IActionResult Edit()
        {
            return View();
        }
        
    }
}
