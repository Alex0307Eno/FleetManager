using isRock.LineBot;

namespace LineBotService.Handlers
{
    public class PostbackHandler
    {
        private readonly ApplicantPostbackHandler _applicantHandler;
        private readonly ManagerReviewHandler _managerHandler;

        public PostbackHandler(ApplicantPostbackHandler applicantHandler, ManagerReviewHandler managerHandler)
        {
            _applicantHandler = applicantHandler;
            _managerHandler = managerHandler;
        }

        public async Task HandlePostbackAsync(dynamic e, string replyToken, string userId)
        {
            var data = (string)e.postback.data ?? "";

            if (data.StartsWith("action=setPassengerCount")
             || data.StartsWith("action=setTripType")
             || data.StartsWith("action=confirmApplication"))
            {
                await _applicantHandler.HandleAsync(e, replyToken, userId, data);
            }
            else if (data.StartsWith("action=reviewApprove")
                  || data.StartsWith("action=reviewReject")
                  || data.StartsWith("action=selectDriver")
                  || data.StartsWith("action=reviewListPage"))

            {
                await _managerHandler.HandleAsync(e, replyToken, userId, data);
            }
            else
            {
                Console.WriteLine($"⚠️ 未知的 Postback action: {data}");
            }
        }
    }
}
