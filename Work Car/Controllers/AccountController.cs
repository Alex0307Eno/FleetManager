using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Cars.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Cars.Models;

namespace Cars.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _db;
        public AccountController(ApplicationDbContext db) { _db = db; }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uid)) return RedirectToAction("Login");

            int userId = int.Parse(uid);
            var user = await _db.Users.FirstOrDefaultAsync(x => x.UserId == userId);
            var applicant = await _db.Applicants.FirstOrDefaultAsync(x => x.UserId == userId);

            var vm = new Profile
            {
                UserId = userId,
                Account = user?.Account,
                DisplayName = user?.DisplayName,
                Dept = applicant?.Dept,
                Ext = applicant?.Ext,
                Email = applicant?.Email,
                Birth = applicant?.Birth
            };
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(Profile vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var user = await _db.Users.FirstOrDefaultAsync(x => x.UserId == vm.UserId);
            if (user != null) user.DisplayName = vm.DisplayName?.Trim();

            var applicant = await _db.Applicants.FirstOrDefaultAsync(x => x.UserId == vm.UserId);
            if (applicant != null)
            {
                applicant.Email = vm.Email?.Trim();
                applicant.Ext = vm.Ext?.Trim();
                applicant.Dept = vm.Dept?.Trim();
                applicant.Birth = vm.Birth;
            }

            await _db.SaveChangesAsync();
            TempData["ok"] = "已更新個人資料";
            return RedirectToAction(nameof(Profile));
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
        }

        [AllowAnonymous]
        public IActionResult Login()
        {
            return View();
        }

        
    }
}
