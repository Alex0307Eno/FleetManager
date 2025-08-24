using Microsoft.AspNetCore.Mvc;

namespace Cars.Controllers
{
    // /api/dashboard/drivers/live?count=5
    [ApiController]
    [Route("api/dashboard/drivers")]
    public class DriversController : ControllerBase
    {
        // TODO: 注入你的 DbContext 或位置來源服務
        [HttpGet("live")]
        public IActionResult Live([FromQuery] int count = 5)
        {
            // 範例資料（請換成實際來源）
            var now = DateTimeOffset.UtcNow;
            var result = new[]
  {
    new { driverId=1, driverName="王○○", plateNo="0980", status="執勤中",
          lat=(double?)25.04012, lng=(double?)121.56491, location="台北市政府", updatedAt=now },
    new { driverId=2, driverName="黃○○", plateNo="6316", status="執勤中",
          lat=(double?)25.04185, lng=(double?)121.56372, location="市府轉運站", updatedAt=now },
    new { driverId=3, driverName="林○○", plateNo=(string?)null, status="待機中",
          lat=(double?)null, lng=(double?)null, location="農業部林業署", updatedAt=now },
    new { driverId=4, driverName="邱○○", plateNo="2061", status="執勤中",
          lat=(double?)25.03455, lng=(double?)121.56731, location="信義區", updatedAt=now },
    new { driverId=5, driverName="吳○○", plateNo=(string?)null, status="待機中",
          lat=(double?)25.03777, lng=(double?)121.56310, location="中正區", updatedAt=now },
}.Take(Math.Clamp(count, 1, 5));

            return Ok(result);
        }
    }

}
