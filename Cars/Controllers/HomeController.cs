using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cars.ApiControllers
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
