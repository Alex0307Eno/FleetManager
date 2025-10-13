using Cars.Data;
using Cars.Models;
using Cars.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cars.Areas.Admin.Controllers
{
    [Authorize]
    [Authorize(Roles = "Admin")]

    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _db;
        public UsersController(ApplicationDbContext db) { _db = db; }

        #region admin 菜單：使用者列表
        [Authorize]

        public async Task<IActionResult> Index(string? q, string? role, bool? active)
        {
            var users = _db.Users.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                users = users.Where(u =>
                    u.Account.Contains(q) ||
                    (u.DisplayName != null && u.DisplayName.Contains(q)));
            }

            if (!string.IsNullOrWhiteSpace(role))
                users = users.Where(u => u.Role == role);

            if (active.HasValue)
                users = users.Where(u => u.IsActive == active.Value);

            var list = await users
                .OrderBy(u => u.UserId)
                .ToListAsync();

            ViewBag.Query = q;
            ViewBag.Role = role;
            ViewBag.Active = active;
            return View(list);
        }

        #endregion

        #region admin 新增使用者
        [HttpGet]
        public IActionResult Create()
        {
            return View(new UserVm());
        }

        // POST: /Admin/Users/Create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserVm vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var exists = await _db.Users.AnyAsync(x => x.Account == vm.Account);
            if (exists)
            {
                ModelState.AddModelError(nameof(vm.Account), "帳號已存在");
                return View(vm);
            }

            var user = new User
            {
                Account = vm.Account.Trim(),
                DisplayName = vm.DisplayName?.Trim(),
                Role = vm.Role?.Trim() ?? "User",
                IsActive = vm.IsActive,
                CreatedAt = DateTime.UtcNow,
                PasswordHash = !string.IsNullOrEmpty(vm.Password)
                ? BCrypt.Net.BCrypt.HashPassword(vm.Password)
                : BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"))
            };

            _db.Users.Add(user);
            var (ok, err) = await _db.TrySaveChangesAsync(this);
            if (!ok) return err!;
            TempData["ok"] = "使用者已建立";
            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region admin 編輯使用者
        public async Task<IActionResult> Edit(int id)
        {
            var u = await _db.Users.FindAsync(id);
            if (u == null) return NotFound();

            var vm = new UserVm
            {
                UserId = u.UserId,
                Account = u.Account,
                DisplayName = u.DisplayName,
                Role = u.Role,
                IsActive = u.IsActive
            };
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UserVm vm)
        {
            if (id != vm.UserId) return BadRequest();
            if (!ModelState.IsValid) return View(vm);

            var u = await _db.Users.FindAsync(id);
            if (u == null) return NotFound();

            if (!string.Equals(u.Account, vm.Account, StringComparison.Ordinal))
            {
                var exists = await _db.Users.AnyAsync(x => x.Account == vm.Account && x.UserId != id);
                if (exists)
                {
                    ModelState.AddModelError(nameof(vm.Account), "帳號已存在");
                    return View(vm);
                }
                u.Account = vm.Account.Trim();
            }

            u.DisplayName = vm.DisplayName?.Trim();
            u.Role = vm.Role?.Trim() ?? "User";
            u.IsActive = vm.IsActive;

            if (!string.IsNullOrEmpty(vm.Password))
            {
                u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(vm.Password);
            }

            var (ok, err) = await _db.TrySaveChangesAsync(this);
            if (!ok) return err!;
            TempData["ok"] = "使用者已更新";
            return RedirectToAction(nameof(Index));
        }
        #endregion

        #region admin 刪除使用者
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var u = await _db.Users.FindAsync(id);
            if (u == null) return NotFound();

            _db.Users.Remove(u);
            var (ok, err) = await _db.TrySaveChangesAsync(this);
            if (!ok) return err!;
            TempData["ok"] = "使用者已刪除";
            return RedirectToAction(nameof(Index));
        }
        #endregion

        #region admin 啟用/停用使用者
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var u = await _db.Users.FindAsync(id);
            if (u == null) return NotFound();

            u.IsActive = !u.IsActive;
            var (ok, err) = await _db.TrySaveChangesAsync(this);
            if (!ok) return err!;
            TempData["ok"] = u.IsActive ? "已啟用" : "已停用";
            return RedirectToAction(nameof(Index));
        }
        #endregion

        #region admin 重設密碼
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(int id, string? newPassword)
        {
            var u = await _db.Users.FindAsync(id);
            if (u == null) return NotFound();

            if (string.IsNullOrWhiteSpace(newPassword))
            {
                TempData["err"] = "請輸入新密碼";
                return RedirectToAction(nameof(Edit), new { id });
            }

            u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            var (ok, err) = await _db.TrySaveChangesAsync(this);
            if (!ok) return err!;
            TempData["ok"] = "密碼已重設";
            return RedirectToAction(nameof(Edit), new { id });
        }
    }
    #endregion

        #region ViewModel
    public class UserVm
    {
        public int UserId { get; set; }
        public string Account { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string Role { get; set; } = "User"; 
        public bool IsActive { get; set; } = true;
        public string? Password { get; set; } 
    }
    #endregion
}
