using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cars.Dispatch
{
    public class DispatchesController : Controller
    {
        [Authorize]
        public IActionResult Dispatch()
        {
            return View();
        }
        [Authorize]
        public IActionResult CarApply()
        {
            return View();
        }
        [Authorize]
        public IActionResult Record()
        {
            return View();
        }
    }
}
