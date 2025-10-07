using System.ComponentModel.DataAnnotations;


namespace Cars.Models
{
    public class CarApplicationAudit
    {
        [Key]
        public int AuditId { get; set; } // 主鍵
        public int ApplyId { get; set; } // 用車申請單編號
        public string Action { get; set; } = "";      // Create / Update / Status / Delete
        public string? ByUserName { get; set; } // 執行動作的使用者名稱
        public DateTime At { get; set; }              // 動作時間
        public string? OldValue { get; set; }         // JSON
        public string? NewValue { get; set; }         // JSON
    }
}
