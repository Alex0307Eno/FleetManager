using Microsoft.AspNetCore.Mvc;

namespace Cars.Places
{
    public class PlacesController : Controller
    {
        public IActionResult Place()
        {
            return View();
        }
    }
}
