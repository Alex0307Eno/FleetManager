using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Cars.Data;

namespace Cars.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ApplicantsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public ApplicantsController(ApplicationDbContext db) { _db = db; }

        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized(new { message = "尚未登入" });
            if (!int.TryParse(userIdStr, out var userId)) return Unauthorized(new { message = "無效的使用者" });

            // 從 Applicants 撈完整資料（欄位名稱請照你的實際模型調整）
            var me = await _db.Applicants
                .Where(a => a.UserId == userId)
                .Select(a => new {
                    name = a.Name,
                    birth = a.Birth,            
                    dept = a.Dept,
                    ext = a.Ext,
                    email = a.Email
                })
                .FirstOrDefaultAsync();

            if (me != null) return Ok(me);

            // 沒有 Applicants 記錄時，退回 Users 表提供基本資訊
            var fallback = await _db.Users
                .Where(u => u.UserId == userId)
                .Select(u => new {
                    name = u.DisplayName ?? u.Account,
                    birth = "",
                    dept = "",
                    ext = "",
                    email = ""
                })
                .FirstOrDefaultAsync();

            return Ok(fallback ?? new { name = "", birth = "", dept = "", ext = "", email = "" });
        }
    }
}
