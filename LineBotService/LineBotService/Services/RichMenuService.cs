using Line.Messaging;
using System.Net.Http.Headers;
using System.Text.Json;

namespace LineBotDemo.Services
{
    public class RichMenuService
    {
        private readonly HttpClient _http;
        private readonly string _token;
        private readonly IConfiguration _config;
        private readonly Dictionary<string, string> _roleToMenu;   // 讀自 appsettings
        private static readonly Dictionary<string, string> RoleAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            // 正式鍵
            ["Driver"] = "Driver",
            ["Admin"] = "Admin",
            ["Applicant"] = "Applicant",
            ["Manager"] = "Admin",



        };

        public string? GetRichMenuIdByRole(string roleInput)
        {
            if (string.IsNullOrWhiteSpace(roleInput)) return null;

            // 跟 BindUserToRoleAsync 用同一個角色對照表
            var key = ResolveRoleKey(roleInput);
            if (key is null) return null;

            return _roleToMenu.TryGetValue(key, out var id) ? id : null;
        }


        public RichMenuService(IConfiguration config, IConfiguration cfg, IHttpClientFactory factory)
        {
            _config = config;
            _http = factory.CreateClient();
            _token = cfg["LineBot:ChannelAccessToken"]
                     ?? throw new ArgumentNullException("LineBot:ChannelAccessToken missing");

            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _token);

            _roleToMenu = cfg.GetSection("RichMenus").Get<Dictionary<string, string>>()
                         ?? throw new ArgumentNullException("RichMenus config missing");
        }

        private string? ResolveRoleKey(string roleInput)
        {
            if (string.IsNullOrWhiteSpace(roleInput)) return null;
            return RoleAliases.TryGetValue(roleInput.Trim(), out var key) ? key : null;
        }

        public async Task<string> CreateRichMenuAsync(string json)
        {
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var res = await _http.PostAsync("https://api.line.me/v2/bot/richmenu", content);
            var body = await res.Content.ReadAsStringAsync();
            return res.IsSuccessStatusCode ? $"✅ 建立成功：{body}" : $"❌ 建立失敗：{body}";
        }

        public async Task<string> BindToUserAsync(string userId, string richMenuId)
        {
            var url = $"https://api.line.me/v2/bot/user/{userId}/richmenu/{richMenuId}";
            var res = await _http.PostAsync(url, null);
            var body = await res.Content.ReadAsStringAsync();
            return res.IsSuccessStatusCode ? "✅ 已綁定" : $"❌ 綁定失敗：{body}";
        }

        public async Task<string> BindUserToRoleAsync(string userId, string roleInput)
        {
            var key = ResolveRoleKey(roleInput);
            if (key is null) return $"❌ 未知角色：{roleInput}（可用：Driver/Admin/Applicant 或別名 User/申請人/駕駛/管理員）";
            if (!_roleToMenu.TryGetValue(key, out var menuId) || string.IsNullOrEmpty(menuId))
                return $"❌ 設定檔未找到角色 {key} 對應的 richMenuId";

            return await BindToUserAsync(userId, menuId);
        }

        public async Task<string> UnbindUserAsync(string userId)
        {
            var url = $"https://api.line.me/v2/bot/user/{userId}/richmenu";
            var res = await _http.DeleteAsync(url);
            var body = await res.Content.ReadAsStringAsync();
            return res.IsSuccessStatusCode ? "✅ 已解除綁定" : $"❌ 解除失敗：{body}";
        }

        public async Task<string> UploadRichMenuImageAsync(string richMenuId, string imagePath)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _config["LineBot:ChannelAccessToken"]);

            using var fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read);
            var content = new StreamContent(fs);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/png"); // 或 "image/jpeg"

            var response = await client.PostAsync(
                $"https://api-data.line.me/v2/bot/richmenu/{richMenuId}/content",
                content);

            var responseBody = await response.Content.ReadAsStringAsync();

            // 新增錯誤輸出，方便除錯
            if (!response.IsSuccessStatusCode)
            {
                return $"❌ 上傳失敗，狀態碼：{response.StatusCode}\n內容：{responseBody}";
            }

            return $"✅ 上傳成功：{responseBody}";
        }
        public async Task<string> DeleteRichMenuAsync(string richMenuId)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _config["LineBot:ChannelAccessToken"]);

            var res = await client.DeleteAsync($"https://api.line.me/v2/bot/richmenu/{richMenuId}");
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                return $"❌ 刪除失敗，狀態碼：{res.StatusCode}\n內容：{body}";
            }

            return $"✅ 已刪除 RichMenu {richMenuId}";
        }
        
        public async Task<string?> GetUserLinkedRichMenuAsync(string userId)
        {
            var res = await _http.GetAsync($"https://api.line.me/v2/bot/user/{userId}/richmenu");
            if (res.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            res.EnsureSuccessStatusCode();
            var body = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("richMenuId", out var el) ? el.GetString() : null;
        }
        /// <summary>
        /// 綁定 RichMenu 給指定使用者
        /// </summary>
        public async Task BindRichMenuToUserAsync(string userId, string richMenuId)
        {
            var token = _config["LineBot:ChannelAccessToken"];
            if (string.IsNullOrEmpty(token))
                throw new InvalidOperationException("❌ ChannelAccessToken 未設定");

            var url = $"https://api.line.me/v2/bot/user/{userId}/richmenu/{richMenuId}";

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var res = await _http.SendAsync(req);
            res.EnsureSuccessStatusCode(); // 如果失敗會丟例外
        }
    }
}
