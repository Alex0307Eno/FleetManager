
namespace Cars.Features.Drivers
{
    public record BulkSetDto
    {
        public List<DateTime> Dates { get; set; } = new();  // 要套用的日期清單（日期即可）
        public PersonAssignDto Assign { get; set; } = new();    // A~E 對應 AM/PM/G1/G2/G3
    }
}
