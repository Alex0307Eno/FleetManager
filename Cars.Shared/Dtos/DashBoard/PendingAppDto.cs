namespace Cars.Shared.Dtos.DashBoard
{
    public record PendingAppDto(
    int ApplyId,
    string UseDate,
    string UseTime,
    string Route,
    string ApplyReason,
    string ApplicantName,
    int PassengerCount,
    string TripDistance,
    string TripDuration,
    string Status
);

}
