using Microsoft.AspNetCore.Mvc;

namespace Cars.Dispatch
{
    public class DispatchController : Controller
    {
        public IActionResult Dispatch()
        {
            return View();
        }
        public IActionResult CarApply()
        {
            return View();
        }

    }
}
