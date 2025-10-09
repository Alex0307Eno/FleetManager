using System.ComponentModel.DataAnnotations;

namespace Cars.Models
{

    public class Record
    {
        [Key]
        public int DispatchId { get; set; }               // 派車單編號 (主鍵)

        public int ApplyId { get; set; }                  // 對應的用車申請單編號

        public DateTime? UseStart { get; set; }           // 用車開始時間

        public DateTime? UseEnd { get; set; }             // 用車結束時間

        public string? Route { get; set; }                // 行車路線（起訖地點或合併後的摘要）

        public string TripType { get; set; }              // 行程類型：單程 / 來回

        public string? ReasonType { get; set; }           // 事由類型

        public string? Reason { get; set; }               // 事由詳細描述

        public string? Applicant { get; set; }            // 申請人姓名

        public int? Seats { get; set; }                   // 乘客人數（申請人+同行人數）

        public decimal? Km { get; set; }                  // 行駛里程（公里數）

        public string? Status { get; set; } = "待派車";   // 狀態：待派車 / 已派車 

        public string? Driver { get; set; }               // 駕駛姓名

        public int? DriverId { get; set; }                // 駕駛 ID

        public string? Plate { get; set; }                // 車輛車牌號碼

        public int? VehicleId { get; set; }               // 車輛 ID

        public string? LongShort { get; set; }            // 長差 / 短差 標記（依里程判定）

        public int? OdometerStart { get; set; }         // 出發時里程數
        public int? OdometerEnd { get; set; }           // 返回時里程數


        public int? ChildDispatchId { get; set; }         // 若為併單：紀錄子單的 DispatchId（否則為 null）
    }
}
