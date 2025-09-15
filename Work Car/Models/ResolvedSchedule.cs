using Microsoft.EntityFrameworkCore;

namespace Cars.Models
{
    [Keyless]
    public class ResolvedSchedule
    {
        public DateTime WorkDate { get; set; }
        public string LineCode { get; set; } = "";
        public string? DriverName { get; set; }
        public string Shifts { get; set; } = ""; // "早、午" 等
    }
}
