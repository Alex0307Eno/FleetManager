using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Cars.Models;

namespace Cars.Controllers
{
    public class HomeController : Controller
    {
        private readonly IConfiguration _config;

        public HomeController(IConfiguration config)
        {
            _config = config;
        }

        public IActionResult Index()
        {
            ViewBag.GoogleMapsKey = _config["GoogleMaps:ApiKey"];
            return View();
        }

        public IActionResult CarApply()
        {
            ViewBag.GoogleMapsKey = _config["GoogleMaps:ApiKey"];
            return View();
        }

        public IActionResult Schedule()
        {
            return View();
        }
       
        public IActionResult FuelStats()
        {
            return View();
        }
    }
}
