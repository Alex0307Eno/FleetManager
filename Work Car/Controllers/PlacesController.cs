using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Cars.Models;
using Microsoft.Extensions.Options;

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

        public class AutocompleteInput { public string Input { get; set; } = ""; }

        [HttpPost("autocomplete")]
        public async Task<IActionResult> Autocomplete([FromBody] AutocompleteInput input)
        {
            if (input == null || string.IsNullOrWhiteSpace(input.Input))
                return BadRequest(new { error = "input is required" });

            var client = _httpClientFactory.CreateClient();
            var url = "https://places.googleapis.com/v1/places:autocomplete";

            var json = $@"{{
      ""input"": ""{input.Input.Replace("\"", "\\\"")}"",
      ""languageCode"": ""zh-TW"",
      ""regionCode"": ""TW""
    }}";

            var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            req.Headers.Add("X-Goog-Api-Key", _settings.ApiKey);

            // ✅ 修改 FieldMask
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

    }
}