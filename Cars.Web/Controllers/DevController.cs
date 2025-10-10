using Cars.Services.GPS;
using Microsoft.AspNetCore.Mvc;

namespace Cars.Controllers
{
    [ApiController]
    [Route("api/dev")]
    public class DevController : ControllerBase
    {
        private readonly GpsLoggerService _logger;
        private readonly IGpsProvider _gps;

        public DevController(GpsLoggerService logger, IGpsProvider gps)
        {
            _logger = logger;
            _gps = gps;
        }

        // 直接從 provider 取得位置並寫入 DB
        [HttpGet("simulate-location/{vehicleId}")]
        public async Task<IActionResult> SimulateLocation(int vehicleId)
        {
            // 直接寫 DB（和 GpsLoggerService 行為一致）
            var loc = await _gps.GetLocationAsync(vehicleId);

            // 若你想重用 GpsLoggerService:
            // await _logger.LogAsync(vehicleId);

            return Ok(loc);
        }

        // 直接觸發 Logger 寫入 DB
        [HttpGet("log-location/{vehicleId}")]
        public async Task<IActionResult> LogLocation(int vehicleId)
        {
            await _logger.LogAsync(vehicleId);
            return Ok(new { ok = true });
        }
    }

}
