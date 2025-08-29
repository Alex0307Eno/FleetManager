using Microsoft.AspNetCore.Mvc;

namespace Cars.Controllers
{
    public class AccountController : Controller
    {
        [HttpGet("/Account/Login")]
        public IActionResult Login() => View();

        [HttpPost("/Account/Logout")]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}
