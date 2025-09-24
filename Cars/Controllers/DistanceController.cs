using Cars.Models;
using Cars.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Cars.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DistanceController : ControllerBase
    {
        private readonly IDistanceService _distance;

        public DistanceController(IDistanceService distance)
        {
            _distance = distance;
        }

        [HttpGet]
        public async Task<IActionResult> GetDistance([FromQuery] string origin, [FromQuery] string destination)
        {
            if (string.IsNullOrWhiteSpace(origin) || string.IsNullOrWhiteSpace(destination))
                return BadRequest(new { error = "origin 和 destination 必填" });

            try
            {
                var (km, minutes) = await _distance.GetDistanceAsync(origin, destination);
                return Ok(new { origin, destination, distanceKm = km, durationMin = minutes });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }

}
