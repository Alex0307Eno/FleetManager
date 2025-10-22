using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Cars.Shared.Dtos
{
    // 傳入 AI 建議派車 API 的請求格式
    public class SuggestInput
    {
        [JsonPropertyName("pending")]
        public IEnumerable<object> Pending { get; set; } = Enumerable.Empty<object>();

        [JsonPropertyName("drivers")]
        public IEnumerable<object> Drivers { get; set; } = Enumerable.Empty<object>();

        [JsonPropertyName("locations")]
        public IEnumerable<object> Locations { get; set; } = Enumerable.Empty<object>();
    }

    // 前端回傳略過建議的時候用
    public class SuggestDecision
    {
        public string SuggestionId { get; set; }
    }

    // AI 生成的建議結果 DTO
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
