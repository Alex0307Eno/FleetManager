using Cars.Models;
using Cars.Services;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

public class GoogleDistanceService : IDistanceService
{
    private readonly GoogleMapsSettings _settings;
    private readonly HttpClient _http;

    public GoogleDistanceService(HttpClient http, IOptions<GoogleMapsSettings> settings)
    {
        _http = http;
        _settings = settings.Value;
    }

    public async Task<(decimal km, double minutes)> GetDistanceAsync(string origin, string destination)
    {
        var url = $"https://maps.googleapis.com/maps/api/distancematrix/json" +
                  $"?units=metric" +
                  $"&origins={Uri.EscapeDataString(origin)}" +
                  $"&destinations={Uri.EscapeDataString(destination)}" +
                  $"&key={_settings.ApiKey}";

        var res = await _http.GetAsync(url);
        var body = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            throw new Exception("Google API 查詢失敗: " + body);

        var json = JObject.Parse(body);
        var elem = json["rows"]?[0]?["elements"]?[0];
        if (elem?["status"]?.ToString() != "OK")
            throw new Exception("查無路線");

        decimal km = (decimal)(elem["distance"]?["value"]?.Value<double>() / 1000 ?? 0);
        double minutes = elem["duration"]?["value"]?.Value<double>() / 60 ?? 0;

        return (km, minutes);
    }
}
