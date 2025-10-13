using System;
using System.Collections.Generic;

namespace Cars.Shared.Dtos.CarApplications
{
    public class CarApplicationDetailDto
    {
        public int ApplyId { get; set; }
        public DateTime UseStart { get; set; }
        public DateTime UseEnd { get; set; }
        public string? Origin { get; set; }
        public string? Destination { get; set; }
        public int PassengerCount { get; set; }
        public string? TripType { get; set; }
        public decimal? SingleDistance { get; set; }
        public decimal? RoundTripDistance { get; set; }
        public string? Status { get; set; }
        public string? ReasonType { get; set; }
        public string? ApplyReason { get; set; }
        public string? MaterialName { get; set; }

        // 車輛/駕駛
        public int? DriverId { get; set; }
        public string? DriverName { get; set; }
        public int? VehicleId { get; set; }
        public string? PlateNo { get; set; }
        public int? Capacity { get; set; }

        // 申請人
        public ApplicantDto? Applicant { get; set; }

        // 乘客
        public List<CarPassengerDto> Passengers { get; set; } = new();
    }

    public class ApplicantDto
    {
        public int ApplicantId { get; set; }
        public string? Name { get; set; }
        public string? Dept { get; set; }
        public string? Email { get; set; }
        public string? Ext { get; set; }
        public DateTime? Birth { get; set; }
    }

   

}
