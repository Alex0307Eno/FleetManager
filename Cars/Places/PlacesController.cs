using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cars.Places
{
    public class PlacesController : Controller
    {
        private readonly IConfiguration _config;
        public PlacesController(IConfiguration config)
        {
            _config = config;
        }
        [Authorize]

        public IActionResult Place()
        {
            ViewBag.GoogleMapsKey = _config["GoogleMaps:ApiKey"];

            return View();
        }
    }
}
