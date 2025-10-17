using Cars.Application.Services;
using Cars.Data;
using isRock.LineBot.RichMenu;
using LineBotService.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LineBotService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RichMenuController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly RichMenuService _service;
        private readonly ApplicationDbContext _db;

        public RichMenuController(IConfiguration config, RichMenuService service, ApplicationDbContext db)
        {
            _config = config;
            _service = service;
            _db = db;
        }

        // ========= 建立 RichMenu =========
        [HttpPost("create-applicant")]
        public async Task<IActionResult> CreateApplicantMenu()
    => Ok(await _service.CreateRichMenuFromFileAsync("wwwroot/json/RichMenu/ApplicantMenu.json"));

        [HttpPost("create-admin")]
        public async Task<IActionResult> CreateAdminMenu()
            => Ok(await _service.CreateRichMenuFromFileAsync("wwwroot/json/RichMenu/AdminMenu.json"));

        [HttpPost("create-driver")]
        public async Task<IActionResult> CreateDriverMenu()
            => Ok(await _service.CreateRichMenuFromFileAsync("wwwroot/json/RichMenu/DriverMenu.json"));

        [HttpPost("finish-driver")]
        public async Task<IActionResult> FinishDriverMenu()
            => Ok(await _service.CreateRichMenuFromFileAsync("wwwroot/json/RichMenu/DriverFinishMenu.json"));


        // ========= 查詢與綁定 =========
        [HttpGet("list")]
        public async Task<IActionResult> GetRichMenuList()
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Bearer", _config["LineBot:ChannelAccessToken"]);

            var response = await client.GetAsync("https://api.line.me/v2/bot/richmenu/list");
            var json = await response.Content.ReadAsStringAsync();
            return Content(json, "application/json");
        }

        [HttpPost("bind/{userId}/{richMenuId}")]
        public async Task<IActionResult> BindUser(string userId, string richMenuId)
            => Ok(await _service.BindToUserAsync(userId, richMenuId));

        [HttpDelete("unbind/{userId}")]
        public async Task<IActionResult> Unbind(string userId)
            => Ok(await _service.UnbindUserAsync(userId));

        [HttpGet("id-by-role/{role}")]
        public IActionResult GetIdByRole(string role)
        {
            var id = _service.GetRichMenuIdByRole(role);
            return id is null
                ? NotFound($"找不到角色 {role} 的 RichMenu 設定")
                : Ok(new { role, richMenuId = id });
        }

        [HttpPost("bind-role/{userId}/{role}")]
        public async Task<IActionResult> BindByRole(string userId, string role)
            => Ok(await _service.BindUserToRoleAsync(userId, role));

        // ========= 強制綁定 =========
        [HttpPost("force-bind/{account}")]
        public async Task<IActionResult> ForceBind(string account, bool unbindFirst = false)
        {
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Account == account || u.UserId.ToString() == account);

            if (user == null)
                return NotFound($"❌ 找不到帳號 {account}");

            if (string.IsNullOrEmpty(user.LineUserId))
                return BadRequest("⚠️ 此使用者尚未綁定 LineUserId");

            var rawRole = string.IsNullOrEmpty(user.Role) ? "Applicant" : user.Role;
            var roleMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Admin", "Admin" }, { "Manager", "Admin" },
                { "Driver", "Driver" }, { "Applicant", "Applicant" }
            };

            var role = roleMap.TryGetValue(rawRole, out var mapped) ? mapped : "Applicant";
            var richMenuId = _service.GetRichMenuIdByRole(role);

            if (string.IsNullOrEmpty(richMenuId))
                return NotFound($"❌ 缺少 {role} 對應的 RichMenuId");

            string unbindResult = null;
            if (unbindFirst)
                unbindResult = await _service.UnbindUserAsync(user.LineUserId);

            var bindResult = await _service.BindToUserAsync(user.LineUserId, richMenuId);

            return Ok(new
            {
                account = user.Account,
                user.LineUserId,
                rawRole,
                normalizedRole = role,
                richMenuId,
                unbindFirst,
                unbindResult,
                bindResult
            });
        }

        // ========= 圖片處理 =========
        [HttpPost("upload-image/{richMenuId}")]
        public async Task<IActionResult> UploadImage(string richMenuId, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("⚠️ 請選擇一張圖片");

            var temp = Path.GetTempFileName();
            await using (var stream = new FileStream(temp, FileMode.Create))
                await file.CopyToAsync(stream);

            var result = await _service.UploadRichMenuImageAsync(richMenuId, temp);
            System.IO.File.Delete(temp);

            return Ok(result);
        }

        [HttpGet("download-image/{richMenuId}")]
        public async Task<IActionResult> DownloadImage(string richMenuId)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Bearer", _config["LineBot:ChannelAccessToken"]);

            var res = await client.GetAsync($"https://api-data.line.me/v2/bot/richmenu/{richMenuId}/content");
            if (!res.IsSuccessStatusCode)
            {
                var error = await res.Content.ReadAsStringAsync();
                return BadRequest($"❌ 無法下載圖片：{error}");
            }

            var bytes = await res.Content.ReadAsByteArrayAsync();
            var type = res.Content.Headers.ContentType?.ToString() ?? "image/png";
            return File(bytes, type);
        }

        [HttpDelete("{richMenuId}")]
        public async Task<IActionResult> DeleteRichMenu(string richMenuId)
            => Ok(await _service.DeleteRichMenuAsync(richMenuId));
    }
}
