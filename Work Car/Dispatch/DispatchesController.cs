using Microsoft.AspNetCore.Mvc;

namespace Cars.Dispatch
{
    public class DispatchesController : Controller
    {
        public IActionResult Dispatch()
        {
            return View();
        }
        public IActionResult CarApply()
        {
            return View();
        }
        public IActionResult Record()
        {
            return View();
        }
    }
}
