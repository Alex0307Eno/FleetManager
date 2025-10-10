using Cars.Data;
using isRock.LineBot;
using isRock.LineBot.RichMenu;
using LineBotDemo.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace LineBotDemo.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
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

        /// <summary>
        /// 建立駕駛員選單 (DriverMenu)
        /// </summary>
        [HttpPost("create-driver")]
        public async Task<IActionResult> CreateDriverMenu()
        {
            string json = @"{
              ""size"": { ""width"": 2500, ""height"": 843 },
              ""selected"": false,
              ""name"": ""DriverMenu"",
              ""chatBarText"": ""查看功能選單"",
              ""areas"": [
                {
                  ""bounds"": { ""x"": 0, ""y"": 0, ""width"": 1250, ""height"": 843 },
                  ""action"": { ""type"": ""message"", ""text"": ""我的行程"" }
                },
                {
                  ""bounds"": { ""x"": 1250, ""y"": 0, ""width"": 1250, ""height"": 843 },
                  ""action"": { ""type"": ""message"", ""text"": ""開始行程"" }
                }
              ]
            }";
            return Ok(await _service.CreateRichMenuAsync(json));
        }

        [HttpPost("finish-driver")]
        public async Task<IActionResult> FinishDriverMenu()
        {
            string json = @"{
              ""size"": { ""width"": 2500, ""height"": 843 },
              ""selected"": false,
              ""name"": ""DriverFinishMenu"",
              ""chatBarText"": ""查看功能選單"",
              ""areas"": [
                {
                  ""bounds"": { ""x"": 0, ""y"": 0, ""width"": 1250, ""height"": 843 },
                  ""action"": { ""type"": ""message"", ""text"": ""我的行程"" }
                },
                {
                  ""bounds"": { ""x"": 1250, ""y"": 0, ""width"": 1250, ""height"": 843 },
                  ""action"": { ""type"": ""message"", ""text"": ""結束行程"" }
                }
              ]
            }";
            return Ok(await _service.CreateRichMenuAsync(json));
        }

        /// <summary>
        /// 建立管理員選單 (AdminMenu)
        /// </summary>
        [HttpPost("create-admin")]
        public async Task<IActionResult> CreateAdminMenu()
        {
            string json = @"{
              ""size"": { ""width"": 2500, ""height"": 843 },
              ""selected"": false,
              ""name"": ""AdminMenu"",
              ""chatBarText"": ""查看功能選單"",
              ""areas"": [
              {
                ""bounds"": { ""x"": 0,    ""y"": 0, ""width"": 833, ""height"": 843 },
                ""action"": { ""type"": ""message"", ""text"": ""預約車輛"" }
                        },
              {
                 ""bounds"": { ""x"": 833,  ""y"": 0, ""width"": 834, ""height"": 843 },
                ""action"": { ""type"": ""message"", ""text"": ""我的行程"" }
                        },
              {
                 ""bounds"": { ""x"": 1667, ""y"": 0, ""width"": 833, ""height"": 843 },
                ""action"": { ""type"": ""message"", ""text"": ""待審核"" }
                        }
            ]
            }";
            return Ok(await _service.CreateRichMenuAsync(json));
        }

        /// <summary>
        /// 建立申請人選單 (ApplicantMenu)
        /// </summary>
        [HttpPost("create-applicant")]
        public async Task<IActionResult> CreateApplicantMenu()
        {
            string json = @"{
              ""size"": { ""width"": 2500, ""height"": 843 },
              ""selected"": false,
              ""name"": ""ApplicantMenu"",
              ""chatBarText"": ""查看功能選單"",
              ""areas"": [
                {
                  ""bounds"": { ""x"": 0, ""y"": 0, ""width"": 1250, ""height"": 843 },
                  ""action"": { ""type"": ""message"", ""text"": ""預約車輛"" }
                },
                {
                  ""bounds"": { ""x"": 1250, ""y"": 0, ""width"": 1250, ""height"": 843 },
                  ""action"": { ""type"": ""message"", ""text"": ""我的行程"" }
                }
              ]
            }";
            return Ok(await _service.CreateRichMenuAsync(json));
        }

        /// <summary>
        /// 查看所有 Rich Menu (會回 richMenuId)
        /// </summary>
        [HttpGet("list")]
        public async Task<IActionResult> GetRichMenuList()
        {
            using var client = new HttpClient();
           client.DefaultRequestHeaders.Authorization =
    new System.Net.Http.Headers.AuthenticationHeaderValue(
        "Bearer",
        _config["LineBot:ChannelAccessToken"]
    );


            var response = await client.GetAsync("https://api.line.me/v2/bot/richmenu/list");
            var json = await response.Content.ReadAsStringAsync();
            return Content(json, "application/json");
        }

        /// <summary>
        /// 綁定使用者到指定 Rich Menu
        /// </summary>
        [HttpPost("bind/{userId}/{richMenuId}")]
        public async Task<IActionResult> BindUser(string userId, string richMenuId)
        {
            return Ok(await _service.BindToUserAsync(userId, richMenuId));
        }

        [HttpGet("id-by-role/{role}")]
        public IActionResult GetIdByRole(string role)
        {
            var id = _service.GetRichMenuIdByRole(role);
            return id is null ? NotFound($"找不到角色 {role} 的 rich menu 設定") : Ok(new { role, richMenuId = id });
        }

        [HttpPost("bind-role/{userId}/{role}")]
        public async Task<IActionResult> BindByRole(string userId, string role)
        {
            var result = await _service.BindUserToRoleAsync(userId, role);
            return Ok(result);
        }
        [HttpDelete("unbind/{userId}")]
        public async Task<IActionResult> Unbind(string userId)
        {
            var result = await _service.UnbindUserAsync(userId);
            return Ok(result);
        }
        // RichMenuController.cs
        [HttpPost("force-bind/{account}")]
        public async Task<IActionResult> ForceBind(string account, bool unbindFirst = false)
        {
            // 1) 找使用者（支援帳號或內部 UserId）
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Account == account || u.UserId.ToString() == account);

            if (user == null)
                return NotFound("❌ 找不到帳號 " + account);

            if (string.IsNullOrEmpty(user.LineUserId))
                return BadRequest("⚠️ 此使用者尚未綁定 LineUserId");

            // 2) 讀取角色，沒有就當 Applicant
            var rawRole = string.IsNullOrEmpty(user.Role) ? "Applicant" : user.Role;

            // 3) 角色別名正規化
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "Admin", "Admin" }, { "Manager", "Admin" },
        { "Driver", "Driver" }, 
        { "Applicant", "Applicant" }
    };
            string role;
            if (!map.TryGetValue(rawRole, out role))
                role = "Applicant"; // 不識別一律回退

            // 4) 由角色拿到對應 RichMenuId（你已有此服務）
            var richMenuId = _service.GetRichMenuIdByRole(role);
            if (string.IsNullOrEmpty(richMenuId))
                return NotFound("❌ 設定檔缺少對應 RichMenuId，角色：" + role);

            // 5) 可選：先解除既有個別綁定，避免殘留
            string unbindResult = null;
            if (unbindFirst)
                unbindResult = await _service.UnbindUserAsync(user.LineUserId);

            // 6) 綁定到正確選單
            var bindResult = await _service.BindToUserAsync(user.LineUserId, richMenuId);

            return Ok(new
            {
                account = user.Account,
                lineUserId = user.LineUserId,
                rawRole = rawRole,
                normalizedRole = role,
                targetRichMenuId = richMenuId,
                unbindFirst = unbindFirst,
                unbindResult = unbindResult,
                bindResult = bindResult
            });
        }

        [HttpPost("upload-image/{richMenuId}")]
        public async Task<IActionResult> UploadImage(string richMenuId, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("⚠️ 請選擇一張圖片");

            var tempPath = Path.GetTempFileName();
            using (var stream = new FileStream(tempPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var result = await _service.UploadRichMenuImageAsync(richMenuId, tempPath);

            System.IO.File.Delete(tempPath);

            return Ok(result);
        }
        [HttpGet("download-image/{richMenuId}")]
        public async Task<IActionResult> DownloadImage(string richMenuId)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Bearer",
                    _config["LineBot:ChannelAccessToken"]
                );

            var response = await client.GetAsync($"https://api-data.line.me/v2/bot/richmenu/{richMenuId}/content");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return BadRequest($"❌ 無法下載圖片，錯誤：{error}");
            }

            var contentType = response.Content.Headers.ContentType?.ToString() ?? "image/png";
            var bytes = await response.Content.ReadAsByteArrayAsync();

            return File(bytes, contentType);
        }
        [HttpDelete("{richMenuId}")]
        public async Task<IActionResult> DeleteRichMenu(string richMenuId)
        {
            var result = await _service.DeleteRichMenuAsync(richMenuId);
            return Ok(result);
        }



    }
}
