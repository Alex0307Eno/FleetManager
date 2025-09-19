using Cars.Data;
using Cars.Models;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
namespace Cars.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public AuthController(ApplicationDbContext db)
        {
            _db = db;
        }
        #region 登入驗證
        public class LoginDto
        {
            public string UserName { get; set; }
            public string Password { get; set; }
        }
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            try
            {
                // 0) 基本檢查
                if (dto == null || string.IsNullOrWhiteSpace(dto.UserName) || string.IsNullOrWhiteSpace(dto.Password))
                    return BadRequest(new { message = "帳號或密碼不可為空" });

                // 帳號格式（字母開頭、3~20）
                //if (!Regex.IsMatch(dto.UserName, @"^[A-Za-z][A-Za-z0-9_]{2,19}$"))
                //    return Unauthorized(new { message = "帳號或密碼錯誤" });

                // 1) 撈出同帳號的候選清單（理論上應唯一，但保險）
                var candidates = await _db.Users
                    .AsNoTracking()
                    .Where(u => u.Account == dto.UserName)
                    .ToListAsync();

                if (!candidates.Any())
                {
                    await LogFailedAttempt(dto.UserName);
                    return Unauthorized(new { message = "帳號或密碼錯誤" });
                }

                // 2) 過濾啟用的
                var active = candidates.Where(u => u.IsActive).ToList();
                if (!active.Any())
                    return Unauthorized(new { message = "帳號已停用，請聯絡管理員" });

                // 3) 鎖定檢查（任一個同帳號處於鎖定中就先擋）
                foreach (var u in active)
                {
                    if (u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTime.UtcNow)
                        return Unauthorized(new { message = "帳號已被鎖定，請稍後再試" });
                }

                // 4) 密碼比對（先 BCrypt，再舊明碼 -> 升級）
                User matched = null;

                // 4-1) BCrypt
                foreach (var u in active.Where(x => !string.IsNullOrEmpty(x.PasswordHash) && x.PasswordHash.StartsWith("$2")))
                {
                    try
                    {
                        if (BCrypt.Net.BCrypt.Verify(dto.Password, u.PasswordHash))
                        {
                            matched = u;
                            break;
                        }
                    }
                    catch { /* 忽略破損 hash */ }
                }

                // 4-2) 舊明碼 -> 命中就升級
                if (matched == null)
                {
                    foreach (var u in active.Where(x => !string.IsNullOrEmpty(x.PasswordHash) && !x.PasswordHash.StartsWith("$2")))
                    {
                        if (u.PasswordHash == dto.Password)
                        {
                            var tracked = await _db.Users.FirstAsync(x => x.UserId == u.UserId);
                            tracked.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
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
                            matched = u;
                            break;
                        }
                    }
                }

                // 5) 無命中 → 記錄失敗次數/可能鎖定
                if (matched == null)
                {
                    await LogFailedAttempt(dto.UserName);
                    return Unauthorized(new { message = "帳號或密碼錯誤" });
                }

                // 6) 成功：清除失敗次數與鎖定
                var entityOk = await _db.Users.FirstAsync(u => u.UserId == matched.UserId);
                entityOk.FailedLoginCount = 0;
                entityOk.LockoutEnd = null;
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
                // 7) Cookie 登入
                var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, matched.DisplayName ?? matched.Account),
            new Claim(ClaimTypes.NameIdentifier, matched.UserId.ToString()),
            new Claim(ClaimTypes.Role, matched.Role ?? "User"),
            new Claim("Account", matched.Account)
        };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProps = new AuthenticationProperties
                {
                    IsPersistent = false,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProps);

                HttpContext.Session?.SetString("UserId", matched.UserId.ToString());
                HttpContext.Session?.SetString("UserName", matched.Account);

                var role = matched.Role ?? "User";
                var redirectUrl =
                      role == "Admin" ? Url.Action("Index", "Home")
                    : role == "Driver" ? Url.Action("MySchedule", "Drivers")
                    : role == "Applicant" ? Url.Action("Dispatch", "Dispatches")
                    : role == "Manager" ? Url.Action("Dispatch", "Dispatches")
                    : role == "Agent" ? Url.Action("MySchedule", "Drivers")
                    : Url.Action("Index", "Home");

                return Ok(new
                {
                    message = "登入成功",
                    userId = matched.UserId,
                    userName = matched.Account,
                    displayName = matched.DisplayName,
                    role,
                    redirectUrl
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "伺服器錯誤", error = ex.Message, stack = ex.StackTrace });
            }
        }

        #endregion


        #region 登出導回登入頁面
        [HttpGet("Logout")]
        public async Task<IActionResult> LogoutGet()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
        }
        #endregion

        #region 失敗次數>=5 鎖定15分鐘
        // 記錄失敗嘗試並處理鎖定
        private async Task LogFailedAttempt(string account, int? userId = null)
        {
            var entity = await _db.Users.FirstOrDefaultAsync(u => u.Account == account);
            if (entity != null)
            {
                entity.FailedLoginCount = (entity.FailedLoginCount ?? 0) + 1;
                if (entity.FailedLoginCount >= 5)
                {
                    var taiwanNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
                                        TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time"));
                    entity.LockoutEnd = taiwanNow.AddMinutes(15); // 鎖 15 分鐘 
                }
                await _db.SaveChangesAsync();
            }
        }
        #endregion

    }
}
