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


        #region 個人資料       
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

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // 資料被別人改過 → 可以提示用戶重試
                return Conflict(new { message = "資料已被更新，請重新整理後再試。", detail = ex.Message });
            }
            catch (DbUpdateException ex)
            {
                // 一般資料庫錯誤
                return BadRequest(new { message = "資料儲存失敗，請確認輸入是否正確。", detail = ex.InnerException?.Message ?? ex.Message });
            }
            catch (Exception ex)
            {
                //500 錯誤
                return StatusCode(500, new { message = "伺服器內部錯誤", error = ex.Message });
            }
            TempData["ok"] = "已更新個人資料";
            return RedirectToAction(nameof(Profile));
        }
        #endregion

        #region 登入頁面
        [AllowAnonymous]
        public IActionResult Login()
        {
            return View();
        }
        #endregion

        #region 登出回到登入頁面
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
        }
        #endregion




    }
}
