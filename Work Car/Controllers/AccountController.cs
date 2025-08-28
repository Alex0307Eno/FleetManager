using Microsoft.AspNetCore.Mvc;
using Cars.Data;
using Cars.Models;
using System.Security.Cryptography;
using System.Text;

namespace Cars.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(string userName, string password)
        {
            string hash = GetHash(password);

            var user = _context.Users
                .FirstOrDefault(u => u.UserName == userName && u.PasswordHash == hash);

            if (user != null)
            {
                // 寫入 Session
                HttpContext.Session.SetString("UserName", user.UserName);
                HttpContext.Session.SetString("Role", user.Role ?? "User");

                return RedirectToAction("Index", "Home");
            }

            ViewBag.Error = "帳號或密碼錯誤";
            return View();
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        private string GetHash(string input)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
                return Convert.ToBase64String(bytes);
            }
        }
    }
}
