using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cars.Models
{
    public class CarApplication
    {
        [Key]
        public int ApplyId { get; set; }                // 主鍵
        public string? ApplyFor { get; set; }           // 申請人
        public string? VehicleType { get; set; }        // 車種
        public string? PurposeType { get; set; }        // 用途
        public int ? VehicleId { get; set; }            // 車輛外鍵
        public Vehicle? Vehicle { get; set; }           // 車輛導覽屬性

        public int PassengerCount { get; set; }         // 預計乘車人數
        [Required]
        public DateTime UseStart { get; set; }          // 預計用車起始時間
        [Required]
        public DateTime UseEnd { get; set; }            // 預計用車結束時間

        public int? DriverId { get; set; }              // 預計司機外鍵
        public string? ReasonType { get; set; }         // 申請事由類別
        [MaxLength(200)]
        public string? ApplyReason { get; set; }        // 申請事由
        [Required, MaxLength(200)]
        [RegularExpression(@"^[\u4e00-\u9fa5a-zA-Z0-9\s\-]+$", ErrorMessage = "出發地格式不正確")]
        public string? Origin { get; set; }             // 出發地

        [Required, MaxLength(200)]
        [RegularExpression(@"^[\u4e00-\u9fa5a-zA-Z0-9\s\-]+$", ErrorMessage = "目的地格式不正確")]
        public string? Destination { get; set; }        // 目的地

        [MaxLength(10)]
        [RegularExpression(@"^(單程|來回|single|round)?$", ErrorMessage = "行程類型必須是 單程/來回")]
        public string? TripType { get; set; }           // 單程/來回
        public decimal? SingleDistance { get; set; }    // 單程公里數
        public string? SingleDuration { get; set; }     // 單程車程時間
        public decimal? RoundTripDistance { get; set; } // 來回公里數
        public string? RoundTripDuration { get; set; }  // 來回車程時間
        public bool isLongTrip { get; set; }            // 是否長途

        public string ? MaterialName { get; set; }         // 車上物品清單
        public string Status { get; set; } = "待審核";  // 申請單狀態
        
        
        public Applicant? Applicant { get; set; }       // 申請人導覽屬性

        public int? ApplicantId { get; set; }           // 申請人外鍵
        public Driver? Driver { get; set; }             // 司機導覽屬性

     
        public ICollection<CarPassenger> Passengers { get; set; } = new List<CarPassenger>(); // 乘客清單
        public ICollection<Dispatch>? DispatchOrders { get; set; } = new List<Dispatch>(); // 派車清單
        [NotMapped]
        public ICollection<DispatchLink> Dispatches { get; set; } = new List<DispatchLink>(); // 派車單連結


    }
}
