namespace Cars.Models
{
    public class Profile
    {
        public int UserId { get; set; }          // 主鍵
        public string? Account { get; set; }     // 帳號
        public string? DisplayName { get; set; } // 顯示名稱
        public string? Dept { get; set; }        // 部門
        public string? Ext { get; set; }         // 分機
        public string? Email { get; set; }       // 電子郵件
        public DateTime? Birth { get; set; }     // 生日
    }
}
