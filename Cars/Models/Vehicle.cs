namespace Cars.Models
{
    public class Vehicle
    {
        public int VehicleId { get; set; }               // 主鍵
        public string? PlateNo { get; set; }             // 車牌
        public string? Brand { get; set; }               // 廠牌
        public string? Model { get; set; }               // 車型
        public int? Capacity { get; set; }               // 核定載客數
        public string? Status { get; set; }              // 車輛狀態

        public string? Type { get; set; }                // 車種
        public DateTime? PurchaseDate { get; set; }      // 購置日期
        public decimal? Value { get; set; }              // 車輛價值
        public DateTime? LicenseDate { get; set; }       // 車輛領牌日期
        public DateTime? StartUseDate { get; set; }      // 車輛啟用日期
        public DateTime? InspectionDate { get; set; }    // 車輛下次驗車日期
        public int? EngineCC { get; set; }               // 排氣量
        public string? EngineNo { get; set; }            // 引擎號碼
        public int? Year { get; set; }                   // 出廠年份
        public string? Source { get; set; }              // 車輛來源
        public string? ApprovalNo { get; set; }          // 核准文號
        public bool Retired { get; set; } = false;       // 是否報廢



        //  關聯
        public ICollection<Dispatch>? DispatchOrders { get; set; } // 派車清單
    }
}
