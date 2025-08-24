using System.ComponentModel.DataAnnotations;

namespace Cars.Models
{
    /// <summary>加油 / 補機油紀錄</summary>
    public class FuelFillUp
    {
        [Key] public int Id { get; set; }

        public int VehicleId { get; set; }
        public string? PlateNo { get; set; }           // 冗餘存車牌，方便查表

        public DateTime Date { get; set; }             // 加油日期
        public int Odometer { get; set; }              // 當下里程 (km)

        /// <summary>油別：汽油 / 柴油 / 機油</summary>
        public string FuelType { get; set; } = "汽油";

        public decimal Liters { get; set; }            // 公升數（機油也用公升計）
        public decimal? Amount { get; set; }           // 金額（可選）
        public string? Note { get; set; }
    }
}
