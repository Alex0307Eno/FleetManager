using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Cars.Data;
using Cars.Models;

namespace Cars.Areas.Admin.Controllers
{
    [Authorize]
    [Authorize(Roles = "Admin")]

    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _db;
        public UsersController(ApplicationDbContext db) { _db = db; }

        // GET: /Admin/Users
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

        // GET: /Admin/Users/Create
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
            await _db.SaveChangesAsync();

            TempData["ok"] = "使用者已建立";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Admin/Users/Edit/5
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

        // POST: /Admin/Users/Edit/5
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
                u.PasswordHash = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(vm.Password));
            }

            await _db.SaveChangesAsync();
            TempData["ok"] = "使用者已更新";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Admin/Users/Delete/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var u = await _db.Users.FindAsync(id);
            if (u == null) return NotFound();

            _db.Users.Remove(u);
            await _db.SaveChangesAsync();
            TempData["ok"] = "使用者已刪除";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Admin/Users/ToggleActive/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var u = await _db.Users.FindAsync(id);
            if (u == null) return NotFound();

            u.IsActive = !u.IsActive;
            await _db.SaveChangesAsync();
            TempData["ok"] = u.IsActive ? "已啟用" : "已停用";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Admin/Users/ResetPassword/5
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

            u.PasswordHash = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(newPassword));
            await _db.SaveChangesAsync();
            TempData["ok"] = "密碼已重設";
            return RedirectToAction(nameof(Edit), new { id });
        }
    }

    // 簡單 VM（只管理 User；Email/Dept/Ext/Birth 在 Applicant 模組）
    public class UserVm
    {
        public int UserId { get; set; }
        public string Account { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string Role { get; set; } = "User"; // Admin / Driver / User
        public bool IsActive { get; set; } = true;
        public string? Password { get; set; } // create 或 edit 可填
    }
}
