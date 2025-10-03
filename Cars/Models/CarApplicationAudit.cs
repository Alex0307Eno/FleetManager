using System.ComponentModel.DataAnnotations;


namespace Cars.Models
{
    public class CarApplicationAudit
    {
        [Key]
        public int AuditId { get; set; }
        public int ApplyId { get; set; }
        public string Action { get; set; } = "";      // Create / Update / Status / Delete
        public string? ByUserName { get; set; }
        public DateTime At { get; set; }              // 建議存 UTC
        public string? OldValue { get; set; }         // JSON
        public string? NewValue { get; set; }         // JSON
    }
}
