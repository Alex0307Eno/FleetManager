using isRock.LineBot;

namespace LineBotService.Services
{
    public class LinePush : ILinePush
    {
        private readonly string _token;

        public LinePush(IConfiguration cfg)
        {
            _token = cfg["LineBot:ChannelAccessToken"];
        }

        public Task PushAsync(string to, string text)
        {
            var bot = new Bot(_token);
            bot.PushMessage(to, text);
            return Task.CompletedTask;
        }
    }
}
