
namespace Cars.Models
{
    public class DispatchOrder
    {
        public int DispatchId { get; set; }                 // 派車單號
        public int? VehicleId { get; set; }                 // 車輛Id
        public string? PlateNo { get; set; } = "未指派";   // 車牌號碼
        public int? DriverId { get; set; }                  // 司機Id
        public string? DriverName { get; set; } = "未指派"; // 司機名稱
        public int? ApplyId { get; set; }                   // 申請單號
        public int? ApplicantId { get; set; }               // 申請人Id
        public string? ApplicantName { get; set; }          // 申請人姓名
        public string? ApplicantDept { get; set; }          // 申請人部門
        public int? PassengerCount { get; set; }            // 預計乘車人數
        public string? UseDate { get; set; }                // 預計用車日期
        public string? UseTime { get; set; }                // 預計用車時間
        public DateTime? UseStart { get; set; }             // 預計用車起始時間
        public string? Route { get; set; }                  // 預計行車路線
        public string? Reason { get; set; }                 // 申請事由
        public decimal? TripDistance { get; set; }          // 預計行駛公里數
        public string? TripType { get; set; }               // 單程/來回
        public string? Status { get; set; }                 // 派車單狀態
    }
}
