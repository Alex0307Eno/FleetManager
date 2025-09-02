using Cars.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Threading.Tasks;

namespace Cars.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DistanceController : ControllerBase
    {
        private readonly GoogleMapsSettings _settings;
        private readonly IHttpClientFactory _httpClientFactory;

        public DistanceController(IOptions<GoogleMapsSettings> settings, IHttpClientFactory httpClientFactory)
        {
            _settings = settings.Value;
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        public async Task<IActionResult> GetDistance([FromQuery] string origin, [FromQuery] string destination)
        {
            if (string.IsNullOrWhiteSpace(origin) || string.IsNullOrWhiteSpace(destination))
                return BadRequest(new { error = "origin 和 destination 必填" });

            var url = $"https://maps.googleapis.com/maps/api/distancematrix/json" +
                      $"?units=metric" +
                      $"&origins={Uri.EscapeDataString(origin)}" +
                      $"&destinations={Uri.EscapeDataString(destination)}" +
                      $"&key={_settings.ApiKey}";

            var client = _httpClientFactory.CreateClient();
            var res = await client.GetAsync(url);
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return BadRequest(new { error = "Google Distance Matrix 失敗", details = body });

            return Content(body, "application/json");
        }
    }
}
