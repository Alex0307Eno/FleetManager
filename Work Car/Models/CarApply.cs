using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
        public int ? VehicleId { get; set; }
        public Vehicle? Vehicle { get; set; }

        public int PassengerCount { get; set; }
        [Required]
        public DateTime UseStart { get; set; }
        [Required]
        public DateTime UseEnd { get; set; }

        public int? DriverId { get; set; }  
        public string? ReasonType { get; set; }
        public string? ApplyReason { get; set; }
        [Required]
        public string? Origin { get; set; }
        [Required]
        public string? Destination { get; set; }
        public string? TripType { get; set; }
        public string? SingleDistance { get; set; }
        public string? SingleDuration { get; set; }
        public string? RoundTripDistance { get; set; }
        public string? RoundTripDuration { get; set; }

        public string Status { get; set; } = "待審核";

        public ICollection<CarPassenger> Passengers { get; set; } = new List<CarPassenger>();
        public Driver? Driver { get; set; }
        public ICollection<Dispatch>? DispatchOrders { get; set; }

    }
}
