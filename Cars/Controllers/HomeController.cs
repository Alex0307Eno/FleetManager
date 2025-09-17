using Cars.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Cars.Controllers
{
    [Authorize]
    [Authorize(Roles = "Admin")]
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

       
    }
}
