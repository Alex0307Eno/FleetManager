using System.ComponentModel.DataAnnotations;

namespace Cars.Models
{
    public class DispatchAudit
    {
        [Key]
        public int DispatchAuditsId { get; set; }
        public int DispatchId { get; set; }

        // 事件類型：Assign / LinkAdd / LinkRemove / Delete / StatusChange ...
        public string Action { get; set; } = "";

        // 舊值/新值
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }

        // 誰做的、什麼時候
        public string? ByUserId { get; set; }
        public string? ByUserName { get; set; }
        public DateTime At { get; set; } = DateTime.UtcNow;
    }

}
