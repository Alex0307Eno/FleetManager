using System;

namespace Cars.Shared.Dtos.Line
{
    public class BookingStateDto
    {
        public string BindAccount { get; set; }  // 綁定帳號 (Line UserId)
        // === 對話狀態 ===
        public int Step { get; set; }                     // 當前步驟 (1~9)

        public int? ApplicantId { get; set; }            // 申請人 ID

        public string? ApplicantName { get; set; }      // 申請人姓名
        // === 使用者輸入內容 ===
        public string? ReserveType { get; set; }          // 即時預約 or 預訂時間

        public DateTime? DepartureTime { get; set; }        // 出發時間（可選）
        public DateTime? ArrivalTime { get; set; }          // 抵達時間（可選）
        public string? Reason { get; set; }               // 用車事由
        public int? PassengerCount { get; set; }          // 乘客人數
        public string? MaterialName { get; set; }        // 物品名稱    

        public string? Origin { get; set; }               // 出發地
        public string? Destination { get; set; }          // 前往地
        public string? TripType { get; set; }             // 單程 / 來回

        // === 系統判斷資訊 ===
        public int? ApplyId { get; set; }                 // 派車申請單 ID
        public string? Status { get; set; }               // 狀態（待審核、已送出等）
        public string? DriverName { get; set; }           // 已選駕駛
        public string? VehiclePlate { get; set; }         // 已選車輛
        public CarApplications.CarApplicationDto ToCarAppDto()
        {
            return new CarApplications.CarApplicationDto
            {
                ApplicantId = this.ApplicantId ?? 0,
                ApplicantName = this.ApplicantName ?? "未填",
                UseStart = this.DepartureTime ?? DateTime.Now,
                UseEnd = this.ArrivalTime ?? (this.DepartureTime?.AddHours(1) ?? DateTime.Now.AddHours(1)),
                ApplyReason = string.IsNullOrWhiteSpace(this.Reason) ? "未填寫" : this.Reason,
                PassengerCount = this.PassengerCount ?? 1,
                Origin = string.IsNullOrWhiteSpace(this.Origin) ? "未填" : this.Origin,
                Destination = string.IsNullOrWhiteSpace(this.Destination) ? "未填" : this.Destination,
                MaterialName = this.MaterialName ?? string.Empty,
                TripType = string.IsNullOrWhiteSpace(this.TripType) ? "single" : this.TripType
            };
        }


    }
}
    
