using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cars.Shared.Dtos;

namespace Cars.Application.Services
{
    public interface IAiDispatchService
    {
        Task<IEnumerable<SuggestionDto>> BuildSuggestionsAsync(SuggestInput input);
    }

    public class AiDispatchService : IAiDispatchService
    {
        public async Task<IEnumerable<SuggestionDto>> BuildSuggestionsAsync(SuggestInput input)
        {
            var pending = input.Pending.ToList();
            var drivers = input.Drivers.ToList();
            var locs = input.Locations.ToList();

            var results = new List<SuggestionDto>();

            foreach (var p in pending.Take(5))
            {
                foreach (var d in drivers.Take(5))
                {
                    results.Add(new SuggestionDto
                    {
                        SuggestionId = $"{Guid.NewGuid()}",
                        ApplyId = GetInt(p, "applyId"),
                        VehicleId = GetInt(d, "vehicleId"),
                        PlateNo = GetStr(d, "plateNo"),
                        DriverId = GetInt(d, "driverId"),
                        DriverName = GetStr(d, "driverName"),
                        Destination = GetStr(p, "route"),
                        Confidence = 0.7,
                        Notes = "就近指派"
                    });
                }
            }
            await Task.CompletedTask;
            return results.DistinctBy(x => x.SuggestionId).Take(10);
        }

        private static int GetInt(object o, string key)
        {
            var val = o?.GetType().GetProperty(key)?.GetValue(o);
            return val == null ? 0 : Convert.ToInt32(val);
        }

        private static string GetStr(object o, string key)
        {
            return o?.GetType().GetProperty(key)?.GetValue(o)?.ToString() ?? "";
        }
    }

    public class SuggestionDto
    {
        public string SuggestionId { get; set; }
        public int ApplyId { get; set; }
        public int VehicleId { get; set; }
        public string PlateNo { get; set; }
        public int DriverId { get; set; }
        public string DriverName { get; set; }
        public string Destination { get; set; }
        public double Confidence { get; set; }
        public string Notes { get; set; }
    }

}
