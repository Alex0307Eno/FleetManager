using System.ComponentModel.DataAnnotations;

namespace Cars.Models
{
    public class CarApply
    {
        [Key]
        public int ApplyId { get; set; }  // 主鍵

        public string? ApplicantName { get; set; }
        public string? ApplicantEmpId { get; set; }
        public string? ApplicantDept { get; set; }
        public string? ApplicantExt { get; set; }
        public string? ApplicantEmail { get; set; }

        public string? ApplyFor { get; set; }
        public string? VehicleType { get; set; }
        public string? PurposeType { get; set; }
        public int PassengerCount { get; set; }

        public DateTime UseStart { get; set; }
        public DateTime UseEnd { get; set; }

        public int DriverId { get; set; }  
        public string? ReasonType { get; set; }
        public string? ApplyReason { get; set; }
        public int Seats { get; set; }
        public string? Origin { get; set; }
        public string? Destination { get; set; }
        public string? TripType { get; set; }
        public string? SingleDistance { get; set; }
        public string? SingleDuration { get; set; }
        public string? RoundTripDistance { get; set; }
        public string? RoundTripDuration { get; set; }

        public string Status { get; set; } = "待審核";

        public ICollection<CarPassenger> Passengers { get; set; } = new List<CarPassenger>();
        public Driver? Applicant { get; set; }
        public ICollection<Dispatch>? DispatchOrders { get; set; }

    }
}
