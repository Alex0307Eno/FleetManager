using System.Collections.Generic;
using Line.Messaging;
using Microsoft.Extensions.Configuration;

namespace LineBotService.Core.Services
{
    public class LinePush : ILinePush
    {
        private readonly string _token;

        public LinePush(IConfiguration cfg)
        {
            _token = cfg["LineBot:ChannelAccessToken"] ?? string.Empty;
        }

        public async Task PushAsync(string to, string text)
        {
            var client = new LineMessagingClient(_token);

            // 這裡要給「清單」，不是單一物件
            var messages = new List<ISendMessage>
            {
                new TextMessage(text)
            };

            await client.PushMessageAsync(to, messages);
        }
    }
}
