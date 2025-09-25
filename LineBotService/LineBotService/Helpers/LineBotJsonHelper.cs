// Helpers/BotJson.cs
using isRock.LineBot;
using System.Text;
using System.Text.Json;

namespace LineBotService.Helpers
{
    public static class BotJson
    {
        // 統一 JsonSerializer 設定
        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // 屬性小駝峰
            WriteIndented = false
        };

        /// <summary>
        /// 傳入已經符合 LINE Messaging API 規格的物件（例如 text message），自動序列化成陣列
        /// </summary>
        public static Task ReplyAsync(string replyToken, object message, string channelAccessToken)
        {
            if (string.IsNullOrWhiteSpace(replyToken))
                throw new ArgumentException("replyToken 不可為空");

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            string payload;
            if (message is System.Collections.IEnumerable && message is not string)
            {
                payload = JsonSerializer.Serialize(message, jsonOptions);
            }
            else
            {
                payload = "[" + JsonSerializer.Serialize(message, jsonOptions) + "]";
            }

            // isRock 的方法回傳 string → 用 Task.FromResult 包裝
            var res = Utility.ReplyMessageWithJSON(replyToken, payload, channelAccessToken);
            return Task.FromResult(res);
        }

        public static Task ReplyAsync(string replyToken, string jsonMessages, string channelAccessToken)
        {
            if (string.IsNullOrWhiteSpace(jsonMessages))
                throw new ArgumentException("jsonMessages 不可為空");

            string payload = jsonMessages.TrimStart();

            if (!payload.StartsWith("["))
                payload = "[" + jsonMessages + "]";

            try { JsonDocument.Parse(payload); }
            catch (Exception ex)
            {
                throw new Exception($"傳入的 JSON 無法解析：{ex.Message}\n原始：{payload}");
            }

            var res = Utility.ReplyMessageWithJSON(replyToken, payload, channelAccessToken);
            return Task.FromResult(res);
        }

        /// <summary>
        /// Push 給特定使用者
        /// </summary>
        public static async Task PushAsync(string token, string toUserId, object message)
        {
            if (string.IsNullOrWhiteSpace(toUserId))
                throw new ArgumentException("toUserId 不可為空");

            string jsonArray;
            if (message is System.Collections.IEnumerable && message is not string)
                jsonArray = JsonSerializer.Serialize(message, jsonOptions);
            else
                jsonArray = "[" + JsonSerializer.Serialize(message, jsonOptions) + "]";

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

            var body = new
            {
                to = toUserId,
                messages = JsonSerializer.Deserialize<object>(jsonArray)
            };

            var content = new StringContent(
                JsonSerializer.Serialize(body, jsonOptions),
                Encoding.UTF8,
                "application/json"
            );

            var res = await http.PostAsync("https://api.line.me/v2/bot/message/push", content);
            res.EnsureSuccessStatusCode();
        }
    }
}
