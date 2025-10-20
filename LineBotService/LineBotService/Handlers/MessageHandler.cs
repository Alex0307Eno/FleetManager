using isRock.LineBot;

namespace LineBotService.Handlers
{
    public class MessageHandler
    {
        private readonly Bot _bot;
        private readonly BindFlowHandler _bindFlow;
        private readonly BookingHandler _booking;
        private readonly PendingListHandler _pending;
        private readonly DriverTripHandler _driverTrip;
        private readonly TripQueryHandler _tripHandler;

        public MessageHandler(
            Bot bot,
            BindFlowHandler bindFlow,
            BookingHandler booking,
            PendingListHandler pending,
            DriverTripHandler driverTrip,
            TripQueryHandler tripHandler)
        {
            _bot = bot;
            _bindFlow = bindFlow;
            _booking = booking;
            _pending = pending;
            _driverTrip = driverTrip;
            _tripHandler = tripHandler;
        }

        public async Task HandleMessageAsync(dynamic e, string replyToken, string userId)
        {
            var msg = ((string)e.message.text ?? "").Trim();
            // 按處理優先順序依次嘗試處理訊息
            // 1. 綁定流程
            if (await _bindFlow.TryHandleAsync(msg, replyToken, userId)) return;
            // 2. 行程查詢
            if (await _tripHandler.TryHandleTripQueryAsync(msg, replyToken, userId)) return;
            // 3. 預約流程
            if (await _booking.TryHandleAsync(msg, replyToken, userId)) return;
            // 4. 待審核清單
            if (await _pending.TryHandleAsync(msg, replyToken, userId)) return;
            // 5. 司機行程操作
            if (await _driverTrip.TryHandleAsync(msg, replyToken, userId)) return;


            _bot.ReplyMessage(replyToken, "🌀 指令未識別，請輸入『預約車輛』或『開始行程』。");
        }
    }
}
