using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Cars.Data;
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
            if (dto == null || string.IsNullOrWhiteSpace(dto.UserName) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(new { message = "帳號或密碼不可為空" });

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Account == dto.UserName);

            if (user == null)
                return Unauthorized(new { message = "帳號或密碼錯誤" });

            bool valid = false;

            // 1️⃣ 嘗試用 BCrypt 驗證
            try
            {
                valid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
            }
            catch
            {
                valid = false;
            }

            // 2️⃣ 如果 BCrypt 驗證失敗，檢查是不是明碼
            if (!valid && user.PasswordHash == dto.Password)
            {
                // ⚡ 登入成功 → 立即轉成 BCrypt 並更新
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
                await _context.SaveChangesAsync();
                valid = true;
            }

            if (!valid)
                return Unauthorized(new { message = "帳號或密碼錯誤" });

            // ✅ 登入成功 → 存 Session
            HttpContext.Session.SetString("UserId", user.UserId.ToString());
            HttpContext.Session.SetString("UserName", user.Account);

            return Ok(new
            {
                message = "登入成功",
                userId = user.UserId,
                userName = user.Account,
                displayName = user.DisplayName
            });
        }
    }
}
