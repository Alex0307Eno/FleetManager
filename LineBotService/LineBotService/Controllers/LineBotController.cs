using Cars.Data;
using Cars.Application.Services;
using Cars.Shared.Dtos.Line;
using isRock.LineBot;
using Microsoft.AspNetCore.Mvc;
using LineBotService.Handlers;

namespace LineBotService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LineBotController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly Bot _bot;
        private readonly DriverService _driverService;
        private readonly VehicleService _vehicleService;
        private readonly CarApplicationService _carApplicationService;
        private readonly AutoDispatcher _autoDispatcher;
        private readonly MessageHandler _messageHandler;
        private readonly PostbackHandler _postbackHandler;
        private readonly IDistanceService _distance;


        public LineBotController(IConfiguration cfg, ApplicationDbContext db,
            DriverService driverService, VehicleService vehicleService, CarApplicationService carApplicationService, AutoDispatcher autoDispatcher, ApplicantPostbackHandler applicantHandler,
            ManagerReviewHandler managerHandler, IDistanceService distanceService)
        {
            _db = db;
            _bot = new Bot(cfg["LineBot:ChannelAccessToken"]);
            _driverService = driverService;
            _vehicleService = vehicleService;
            _carApplicationService = carApplicationService;
            _autoDispatcher = autoDispatcher;
            _distance = distanceService;
            _messageHandler = new MessageHandler(_bot, _db, _driverService, _vehicleService, _carApplicationService, _distance);
            _postbackHandler = new PostbackHandler(applicantHandler, managerHandler);
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] object raw)
        {
            var body = raw?.ToString() ?? string.Empty;
            Console.WriteLine("Webhook Body:\n" + body);

            var events = Utility.Parsing(body);
            foreach (var e in events.events)
            {
                var replyToken = e.replyToken;
                var userId = e.source?.userId ?? "";
                if (string.IsNullOrEmpty(userId)) continue;

                if (e.type == "message")
                    await _messageHandler.HandleMessageAsync(e, replyToken, userId);

                else if (e.type == "postback")
                    await _postbackHandler.HandlePostbackAsync(e, replyToken, userId);
            }

            return Ok();
        }
    }
}
