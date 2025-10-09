namespace Cars.Features.DashBoard
{
    public record UncompleteDispatchDto(
    string UseDate,
    string UseTime,
    string Route,
    string ApplyReason,
    string ApplicantName,
    int PassengerCount,
    string TripDistance,
    string TripDuration,
    string Status,
    string DispatchStatus,
    string DriverName,
    string PlateNo
);

}
