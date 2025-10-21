using Line.Messaging;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;

namespace LineBotService.Core.Services
{
    public class LinePush : ILinePush
    {
        private readonly string _token;

        public LinePush(IConfiguration cfg)
        {
            _token = cfg["LineBot:ChannelAccessToken"] ?? string.Empty;
        }

        public async Task PushAsync(string to, string flexJson)
        {
            if (string.IsNullOrWhiteSpace(to) || string.IsNullOrWhiteSpace(flexJson))
                return;

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _token);

            object message;

            //  先判斷開頭是不是 JSON 結構
            if (flexJson.TrimStart().StartsWith("{") || flexJson.TrimStart().StartsWith("["))
            {
                try
                {
                    message = JsonConvert.DeserializeObject(flexJson);
                }
                catch
                {
                    // fallback 成純文字
                    message = new { type = "text", text = flexJson };
                }
            }
            else
            {
                // 不是 JSON → 當純文字訊息處理
                message = new { type = "text", text = flexJson };
            }

            var payload = new
            {
                to,
                messages = new[] { message }
            };

            var content = new StringContent(
                JsonConvert.SerializeObject(payload),
                Encoding.UTF8,
                "application/json"
            );

            try
            {
                var res = await client.PostAsync("https://api.line.me/v2/bot/message/push", content);
                var resp = await res.Content.ReadAsStringAsync();

                if (!res.IsSuccessStatusCode)
                {
                    if (res.StatusCode == System.Net.HttpStatusCode.TooManyRequests || resp.Contains("monthly limit"))
                    {
                        Console.WriteLine("⚠️ LINE 推播達月上限，訊息略過。");
                        return;
                    }

                    if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        Console.WriteLine("⚠️ LINE Token 已失效或權限不足。");
                        return;
                    }

                    Console.WriteLine($"⚠️ LINE 推播失敗：{res.StatusCode} / {resp}");
                    return;
                }

                Console.WriteLine($"✅ LINE Push 成功 → {res.StatusCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ LINE 推播例外：{ex.Message}");
            }
        }
    }
}
