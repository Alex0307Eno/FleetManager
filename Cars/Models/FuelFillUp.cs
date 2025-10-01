using System.ComponentModel.DataAnnotations;

namespace Cars.Models
{
    /// <summary>加油 / 補機油紀錄</summary>
    public class FuelFillUp
    {
        [Key]
        public int FuelTransactionId { get; set; }
        public DateTime TxTime { get; set; }          // 交易時間
        public string StationName { get; set; }       // 加油站
        public string CardNo { get; set; }            // 卡號（原始）
        public int? VehicleId { get; set; }           // 透過卡號或車牌比對
        public string PlateNo { get; set; }           // 檔案內車牌（若有）
        public decimal Liters { get; set; }           // 公升
        public decimal UnitPrice { get; set; }        // 單價
        public decimal Amount { get; set; }           // 總額
        public int? Odometer { get; set; }            // 里程（若檔案或當下輸入）
        public string RawHash { get; set; }           // 去重用指紋（卡號+時間+金額）
        public string SourceFileName { get; set; }    // 來源檔
        public DateTime ImportedAt { get; set; }
    }

}
