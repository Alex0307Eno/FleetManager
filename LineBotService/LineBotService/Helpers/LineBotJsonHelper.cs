// Helpers/BotJson.cs
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace LineBotService.Helpers
{
    public static class BotJson
    {
        public static Task ReplyAsync(string token, string replyToken, string jsonArray)
            => Task.Run(() => isRock.LineBot.Utility.ReplyMessageWithJSON(replyToken, jsonArray, token));

        public static async Task PushAsync(string token, string toUserId, string jsonArray)
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            var body = $@"{{ ""to"": ""{toUserId}"", ""messages"": {jsonArray} }}";
            var res = await http.PostAsync("https://api.line.me/v2/bot/message/push",
                        new StringContent(body, Encoding.UTF8, "application/json"));
            res.EnsureSuccessStatusCode();
        }
    }
}
