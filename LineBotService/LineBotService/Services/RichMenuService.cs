using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace LineBotDemo.Services
{
    public class RichMenuService
    {
        private readonly string _channelAccessToken;
        private readonly HttpClient _http;


        public RichMenuService(IConfiguration config)
        {
            _channelAccessToken = config["LineBot:ChannelAccessToken"];
            _http = new HttpClient();
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _channelAccessToken);
        }

        // 建立 Rich Menu
        public async Task<string> CreateRichMenuAsync(string jsonBody)
        {
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync("https://api.line.me/v2/bot/richmenu", content);
            return await response.Content.ReadAsStringAsync();
        }

        // 上傳圖片
        public async Task<string> UploadImageAsync(string richMenuId, string imagePath)
        {
            using var fs = System.IO.File.OpenRead(imagePath);
            using var content = new StreamContent(fs);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");

            var url = $"https://api-data.line.me/v2/bot/richmenu/{richMenuId}/content";
            var response = await _http.PostAsync(url, content);
            return await response.Content.ReadAsStringAsync();
        }

        // 綁定給使用者
        public async Task<string> BindToUserAsync(string userId, string richMenuId)
        {
            var url = $"https://api.line.me/v2/bot/user/{userId}/richmenu/{richMenuId}";
            var response = await _http.PostAsync(url, null);
            return await response.Content.ReadAsStringAsync();
        }
    }
}
