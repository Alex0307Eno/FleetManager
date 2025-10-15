using System;

namespace Cars.Shared.Dtos.CarApplications
{
    public class CarApplicationDto
    {
        public int ApplyId { get; set; }
        public int? VehicleId { get; set; }
        public string? PlateNo { get; set; }
        public int? DriverId { get; set; }
        public string? DriverName { get; set; }
        public int? ApplicantId { get; set; }
        public string ApplicantName { get; set; }
        public string? ApplicantDept { get; set; }
        public int? PassengerCount { get; set; }
        public string? ApplyFor { get; set; }
        public string? VehicleType { get; set; }    
        public string? PurposeType { get; set; }
        public string? TripType { get; set; }
        public DateTime UseStart { get; set; }
        public DateTime UseEnd { get; set; }
        public string? Origin { get; set; }
        public string? Destination { get; set; }
        public bool? IsLongTrip { get; set; }
        public decimal? SingleDistance { get; set; }
        public decimal? RoundTripDistance { get; set; }
        public string? SingleDuration { get; set; }
        public string? RoundTripDuration { get; set; }
        public string? MaterialName { get; set; }
        public string? Status { get; set; }
        public string? ReasonType { get; set; }
        public string? ApplyReason { get; set; }
    }
}
