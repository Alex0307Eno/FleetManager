using Cars.Shared.Dtos.Line;
using isRock.LineBot;

namespace LineBotService.Helpers
{
    public static class LineBotUtils
    {
        public static Dictionary<string, BookingStateDto> Conversations { get; } = new();

        public static void SafeReply(Bot bot, string replyToken, string json)
        {
            try
            {
                if (!json.TrimStart().StartsWith("[")) json = "[" + json + "]";
                bot.ReplyMessageWithJSON(replyToken, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Flex ERROR] {ex.Message}");
            }
        }

        public static void SafePush(Bot bot, string userId, string json)
        {
            try
            {
                if (!json.TrimStart().StartsWith("[")) json = "[" + json + "]";
                bot.PushMessageWithJSON(userId, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Push ERROR] {ex.Message}");
            }
        }
    }
}
