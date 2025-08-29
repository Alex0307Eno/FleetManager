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
            try
            {
                if (dto == null || string.IsNullOrWhiteSpace(dto.UserName) || string.IsNullOrWhiteSpace(dto.Password))
                    return BadRequest(new { message = "帳號或密碼不可為空" });

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Account == dto.UserName);

                if (user == null)
                    return Unauthorized(new { message = "帳號或密碼錯誤" });

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
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "伺服器錯誤", error = ex.Message, stack = ex.StackTrace });
            }
        }

    }
}
