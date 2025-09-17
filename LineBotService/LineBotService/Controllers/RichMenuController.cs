using Microsoft.AspNetCore.Mvc;
using LineBotDemo.Services;
using System.Threading.Tasks;

namespace LineBotDemo.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RichMenuController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly RichMenuService _service;


        public RichMenuController(IConfiguration config, RichMenuService service) 
        {
            _config = config;
            _service = service;
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
                  ""bounds"": { ""x"": 0, ""y"": 0, ""width"": 833, ""height"": 843 },
                  ""action"": { ""type"": ""message"", ""text"": ""預約車輛"" }
                },
                {
                  ""bounds"": { ""x"": 833, ""y"": 0, ""width"": 833, ""height"": 843 },
                  ""action"": { ""type"": ""message"", ""text"": ""我的行程"" }
                },
                {
                  ""bounds"": { ""x"": 1666, ""y"": 0, ""width"": 833, ""height"": 843 },
                  ""action"": { ""type"": ""message"", ""text"": ""待審核"" }
                }
              ]
            }";
            return Ok(await _service.CreateRichMenuAsync(json));
        }

        /// <summary>
        /// 建立申請人選單 (UserMenu)
        /// </summary>
        [HttpPost("create-user")]
        public async Task<IActionResult> CreateUserMenu()
        {
            string json = @"{
              ""size"": { ""width"": 2500, ""height"": 843 },
              ""selected"": false,
              ""name"": ""UserMenu"",
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
            var result = await response.Content.ReadAsStringAsync();
            return Ok(result);
        }

        /// <summary>
        /// 綁定使用者到指定 Rich Menu
        /// </summary>
        [HttpPost("bind/{userId}/{richMenuId}")]
        public async Task<IActionResult> BindUser(string userId, string richMenuId)
        {
            return Ok(await _service.BindToUserAsync(userId, richMenuId));
        }
    }
}
