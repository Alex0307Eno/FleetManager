using Cars.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;
namespace Cars.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AuthController(ApplicationDbContext context)
        {
            _context = context;
        }

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
                if (dto == null || string.IsNullOrWhiteSpace(dto.UserName) || string.IsNullOrWhiteSpace(dto.Password))
                    return BadRequest(new { message = "帳號或密碼不可為空" });

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Account == dto.UserName);

                if (user == null)
                    return Unauthorized(new { message = "帳號或密碼錯誤" });

                if (user.IsActive == false)
                {
                    return Unauthorized(new { message = "帳號已停用，請聯絡管理員" });
                }


                bool valid = false;
                if (!string.IsNullOrEmpty(user.PasswordHash))
                {
                    try
                    {
                        valid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
                    }
                    catch { valid = false; }
                }

                if (!valid && user.PasswordHash == dto.Password)
                {
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
                    await _context.SaveChangesAsync();
                    valid = true;
                }

                if (!valid)
                    return Unauthorized(new { message = "帳號或密碼錯誤" });

                // ===== 加入 ClaimsPrincipal =====
                var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.DisplayName ?? user.Account),
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Role, user.Role ?? "User")
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

                // 如果你還需要 Session，可以保留
                HttpContext.Session.SetString("UserId", user.UserId.ToString());
                HttpContext.Session.SetString("UserName", user.Account);
                var role = user.Role ?? "User";
                string redirectUrl = role == "Admin" ? Url.Action("Index", "Home") :
                                     role == "Driver" ? Url.Action("Record", "Dispatches") :
                                     role == "Applicant" ? Url.Action("Dispatch", "Dispatches") : null;

                if (redirectUrl == null) return Forbid();
                return Ok(new
                {
                    message = "登入成功",
                    userId = user.UserId,
                    userName = user.Account,
                    displayName = user.DisplayName,
                    role = role,
                    redirectUrl = redirectUrl

                });

            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "伺服器錯誤", error = ex.Message, stack = ex.StackTrace });
            }
        }
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            // 登出：清除 cookie 驗證
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // 清除 Session
            HttpContext.Session.Clear();

            // 導回首頁或登入頁
            return RedirectToAction("Login", "Account");
            // 或者直接 return RedirectToAction("Login", "Account");
        }

        [HttpGet("Logout")]
        public async Task<IActionResult> LogoutGet()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
        }

    }
}
