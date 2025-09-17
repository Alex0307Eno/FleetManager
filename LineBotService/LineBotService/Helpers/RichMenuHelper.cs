using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

public class RichMenuHelper
{
    private readonly string channelAccessToken;

    public RichMenuHelper(string token)
    {
        channelAccessToken = token;
    }

    public async Task<string> CreateRichMenuAsync(string jsonBody)
    {
        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", channelAccessToken);

            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("https://api.line.me/v2/bot/richmenu", content);
            var result = await response.Content.ReadAsStringAsync();

            Console.WriteLine("CreateRichMenu result: " + result);
            return result;
        }
    }

    public async Task UploadImageAsync(string richMenuId, string imagePath)
    {
        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", channelAccessToken);

            using (var fs = System.IO.File.OpenRead(imagePath))
            using (var content = new StreamContent(fs))
            {
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");

                var url = $"https://api-data.line.me/v2/bot/richmenu/{richMenuId}/content";
                var response = await client.PostAsync(url, content);
                var result = await response.Content.ReadAsStringAsync();

                Console.WriteLine("UploadImage result: " + result);
            }
        }
    }
}
