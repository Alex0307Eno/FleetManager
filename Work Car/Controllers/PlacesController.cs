using Cars.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Cars.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PlacesController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly GoogleMapsSettings _settings;

        public PlacesController(IHttpClientFactory httpClientFactory, IOptions<GoogleMapsSettings> settings)
        {
            _httpClientFactory = httpClientFactory;
            _settings = settings.Value;
        }

        public class AutocompleteInput { 
            public string Input { get; set; } = "";
            public string? SessionToken { get; set; }  
        }

        [HttpPost("autocomplete")]
        public async Task<IActionResult> Autocomplete([FromBody] AutocompleteInput input)
        {
            if (input == null || string.IsNullOrWhiteSpace(input.Input))
                return BadRequest(new { error = "input is required" });

            var client = _httpClientFactory.CreateClient();
            var url = "https://places.googleapis.com/v1/places:autocomplete";

            // ✅ 改用物件序列化，帶入 sessionToken（可為 null）
            var reqBody = new
            {
                input = input.Input,
                languageCode = "zh-TW",
                regionCode = "TW",
                sessionToken = string.IsNullOrWhiteSpace(input.SessionToken) ? null : input.SessionToken,
                locationRestriction = new
                {
                    rectangle = new
                    {
                        low = new { latitude = 21.8, longitude = 119.3 },  // 台灣最南西
                        high = new { latitude = 25.3, longitude = 122.1 }  // 台灣最北東
                    }
                }
            };
            var json = JsonSerializer.Serialize(reqBody, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            req.Headers.Add("X-Goog-Api-Key", _settings.ApiKey);

            // 只要 id + 顯示文字即可
            req.Headers.Add("X-Goog-FieldMask",
                "suggestions.placePrediction.placeId,suggestions.placePrediction.text.text");

            var res = await client.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                return StatusCode((int)res.StatusCode, new
                {
                    error = "PLACES_AUTOCOMPLETE_FAILED",
                    status = (int)res.StatusCode,
                    body
                });
            }

            return Content(body, "application/json");
        }

        // ✅ 新增 Place Details：用相同 sessionToken 完成這次 session（計費在這裡發生）
        [HttpGet("details/{placeId}")]
        public async Task<IActionResult> Details(string placeId, [FromQuery] string? sessionToken)
        {
            if (string.IsNullOrWhiteSpace(placeId))
                return BadRequest(new { error = "placeId is required" });

            var client = _httpClientFactory.CreateClient();
            var tokenPart = string.IsNullOrWhiteSpace(sessionToken) ? "" : $"?sessionToken={Uri.EscapeDataString(sessionToken)}";
            var url = $"https://places.googleapis.com/v1/places/{Uri.EscapeDataString(placeId)}{tokenPart}";

            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("X-Goog-Api-Key", _settings.ApiKey);

            // 想拿到的欄位（可自行增減）
            req.Headers.Add("X-Goog-FieldMask",
                "id,displayName.text,formattedAddress,location,types");

            var res = await client.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                return StatusCode((int)res.StatusCode, new
                {
                    error = "PLACES_DETAILS_FAILED",
                    status = (int)res.StatusCode,
                    body
                });
            }

            return Content(body, "application/json");
        }

    }
}