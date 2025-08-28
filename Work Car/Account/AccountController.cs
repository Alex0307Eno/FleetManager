using Microsoft.AspNetCore.Mvc;

namespace Cars.Account
{
    public class AccountController : Controller
    {
        public IActionResult Login()
        {
            return View();
        }
    }
}
